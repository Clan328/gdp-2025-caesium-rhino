using Rhino;
using Rhino.Commands;
using System;
using Rhino.Geometry;
using System.Collections.Generic;
using Rhino.DocObjects;
using CesiumAuthentication;
using MessageBox = Eto.Forms.MessageBox;
using MessageBoxButtons = Eto.Forms.MessageBoxButtons;
using MessageBoxType = Eto.Forms.MessageBoxType;
using MessageBoxDefaultButton = Eto.Forms.MessageBoxDefaultButton;

namespace LoadTiles
{
    [Rhino.Commands.CommandStyle(Rhino.Commands.Style.ScriptRunner)]
    public class LoadTilesCommand : Command
    {
        public double latitude, longitude, altitude, renderDistance; // We store the last inputted values so that we can write them to file when saving.
        public CesiumAsset selectedAsset;
        public bool locationInputted = false;
        public TemporaryGeometryConduit displayConduit;
        public AttributionConduit attributionConduit;
        public LoadTilesCommand()
        {
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Fetch";

        /// <summary>
        /// Translates the loaded tiles to the origin in Rhino3d.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="objects">Imported objects</param>
        /// <param name="targetPoint">Point to translate to origin</param>
        /// <param name="lat">Corresponding latitude of targetPoint</param>
        /// <param name="lon">Corresponding longitude of targetPoint</param>
        private List<Guid> TranslateLoadedTiles(RhinoDoc doc, IEnumerable<RhinoObject> objects, Point3d targetPoint, double lat, double lon) {
            double scaleFactor = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            
            // Calculate the three orthogonal vectors for rotation
            Vector3d up = new Vector3d(targetPoint);
            up.Unitize();
            Point3d pointToNorth = lat <= 89 ?
                Helper.LatLonToEPSG4978(lat + 1, lon, 0) :
                Helper.LatLonToEPSG4978(179 - lat, lon + 180, 0);
            Vector3d pointToNorthVec = new Vector3d(pointToNorth);
            Vector3d north = pointToNorthVec - up * pointToNorthVec * up;
            north.Unitize();
            Vector3d east = Vector3d.CrossProduct(north, up);

            // Calculates transformation needed to move to Rhino3d's origin
            Transform toOrigin = Transform.Translation(-targetPoint.X*scaleFactor, -targetPoint.Y*scaleFactor, -targetPoint.Z*scaleFactor);
            Transform rotate = Transform.Rotation(east, north, up, new Vector3d(1,0,0), new Vector3d(0,1,0), new Vector3d(0,0,1));
            Transform moveRot = rotate * toOrigin;

            List<Guid> transformedObjects = new List<Guid>();

            // Applies the transformation to all loaded tiles
            foreach (RhinoObject obj in objects) {
                Guid guid = doc.Objects.Transform(obj, moveRot, true);
                transformedObjects.Add(guid);
            }

            return transformedObjects;
        }

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (!AuthSession.IsLoggedIn) {
                Eto.Forms.DialogResult res = MessageBox.Show("You need to be logged in to Cesium Ion", MessageBoxButtons.OKCancel, MessageBoxType.Question, MessageBoxDefaultButton.OK);

                if (res == Eto.Forms.DialogResult.Ok) {
                    AuthSession.Login(true);
                }
                return Result.Cancel;
            }

            RhinoApp.WriteLine("Fetching...");

            this.attributionConduit = new AttributionConduit();

            LoadTilesGUI dialog = new LoadTilesGUI();
            if (this.locationInputted) {
                dialog.prefillData(this.latitude, this.longitude, this.altitude, this.renderDistance, this.selectedAsset);
            }
            DialogResult result = dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

            if (result == null) {
                RhinoApp.WriteLine("User canceled input.");
                return Result.Cancel;
            }

            // Process the inputs from the dialog window
            string apiKey = result.apiKey;
            this.selectedAsset = result.selectedAsset;
            TileLoader tileLoader = selectedAsset.id switch {
                2275207 => new TileLoaderGoogle(),
                96188 => new TileLoaderCesium(),
                _ => throw new NotImplementedException("Asset not implemented.")
            };

            // Keep track of which objects already exist in the project.
            // This is so that, once we've imported the objects from Google Maps/whatever else, we can delete only these new ones.
            List<Guid> existingObjects = new List<Guid>();
            foreach (var obj in doc.Objects) {
                existingObjects.Add(obj.Id);
            }

            // Configure location to render
            this.latitude = result.latitude;
            this.longitude = result.longitude;
            this.locationInputted = true;
            this.altitude = result.altitude;
            this.renderDistance = result.radius;  // Radius around target point to load.

            // Used to display the objects we import, even though they don't "exist" in the traditional sense in the project.
            this.displayConduit = new TemporaryGeometryConduit();

            Point3d targetPoint = Helper.LatLonToEPSG4978(this.latitude, this.longitude, this.altitude);
            // Load tiles
            List<RhinoObject> objects = tileLoader.LoadTiles(doc, existingObjects, targetPoint, this.renderDistance, apiKey);
            // Move tiles to origin
            List<Guid> transformedObjects = TranslateLoadedTiles(doc, objects, targetPoint, this.latitude, this.longitude);

            doc.Views.ActiveView.ActiveViewport.ZoomExtents();

            // We've imported our objects. We now need to delete them from the active document and instead put them in the TemporaryGeometryConduit.
            foreach (var guid in transformedObjects) {
                var obj = doc.Objects.FindId(guid);
                if (obj == null) continue;
                this.displayConduit.addObject(obj);
                doc.Objects.Delete(obj);
            }

            // Clean up after ourselves. This stops really large file sizes when saving afterwards.
            RhinoApp.RunScript("_-Purge All _Enter", false);

            RhinoApp.WriteLine("Tiles loaded.");

            if (hasMaskingDataBeenFound()) {
                RhinoApp.WriteLine("Applying previous masking...");
                reapplyMaskingFromFile(doc);
                RhinoApp.WriteLine("Previous masking has been applied.");
            }

            return Result.Success;
        }

        private void reapplyMaskingFromFile(RhinoDoc doc) {
            Command[] commands = PlugIn.GetCommands();
            MaskingCommand maskingCommand = null;
            foreach (Command command in commands) {
                if (command.EnglishName == "Mask") {
                    maskingCommand = (MaskingCommand) command;
                }
            }
            if (maskingCommand == null) return;

            maskingCommand.applyMaskingDataFromFile(doc);
        }

        private bool hasMaskingDataBeenFound() {
            Command[] commands = PlugIn.GetCommands();
            MaskingCommand maskingCommand = null;
            foreach (Command command in commands) {
                if (command.EnglishName == "Mask") {
                    maskingCommand = (MaskingCommand) command;
                }
            }
            if (maskingCommand == null) return false;

            return maskingCommand.maskingDataFromFile.Length > 0;
        }
    }
}
