using Rhino;
using Rhino.Commands;
using System;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
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
        public AttributionConduit attributionConduit;

        public LoadTilesCommand()
        {
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Fetch";

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

            attributionConduit = AttributionConduit.Instance;

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
            TemporaryGeometryConduit displayConduit = TemporaryGeometryConduit.Instance;
            displayConduit.Reset();

            Point3d targetPoint = Helper.LatLonToEPSG4978(this.latitude, this.longitude, this.altitude);
            // Load tiles
            List<RhinoObject> objects = tileLoader.LoadTiles(doc, existingObjects, targetPoint, this.renderDistance, apiKey);
            // Move tiles to origin
            List<Guid> transformedObjects = TileLoader.TranslateLoadedTiles(doc, objects, targetPoint, this.latitude, this.longitude);

            doc.Views.ActiveView.ActiveViewport.ZoomExtents();

            // We've imported our objects. We now need to delete them from the active document and instead put them in the TemporaryGeometryConduit.
            foreach (var guid in transformedObjects) {
                var obj = doc.Objects.FindId(guid);
                if (obj == null) continue;
                displayConduit.AddObject(obj);
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
