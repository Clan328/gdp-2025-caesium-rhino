using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Rhino.Geometry.Intersect;
using System.Linq;

namespace LoadTiles;

public class MaskingCommand : Command {
    public override string EnglishName => "SealionMask";

    public List<Guid> maskingObjects = new List<Guid>();
    public List<string> maskingObjectNames = new List<string>();

    public string maskingDataFromFile = "";

    public MaskingCommand() {
        Instance = this;
    }

    public void applyMaskingDataFromFile(RhinoDoc doc) {
        if (maskingDataFromFile.Length == 0) return;

        string[] guids = maskingDataFromFile.Split(",");
        foreach (string guidString in guids) {
            Guid guid;
            string name = "";
            if (guidString.IndexOf(":") == -1) {
                guid = Guid.Parse(guidString);
            } else {
                string[] segments = maskingDataFromFile.Split(":");
                name = segments[0];
                guid = Guid.Parse(segments[1]);
            }
            Result result = performMasking(doc, guid);
            if (result == Result.Success) {
                maskingObjects.Add(guid);
                maskingObjectNames.Add(name);
            }
        }
    }

    public void openDialog(RhinoDoc doc) {
        MaskingDialog maskingDialog = new MaskingDialog(this, doc);
        maskingDialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
    }

    public void promptUserSelection(RhinoDoc doc) {
        var objRef = getObjectFromUser();
        if (objRef == null) return;

        Result result = performMasking(doc, objRef.ObjectId);

        if (result != Result.Success) return;

        maskingObjects.Add(objRef.ObjectId);
        maskingObjectNames.Add("");

        this.openDialog(doc);
    }

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) {
        this.openDialog(doc);

        return Result.Success;
    }

    private ObjRef? getObjectFromUser() {
        var getObject = new GetObject();
        getObject.SetCommandPrompt("Select a masking object");
        getObject.GeometryFilter = ObjectType.Brep;
        getObject.SubObjectSelect = false;
        getObject.DeselectAllBeforePostSelect = true;
        getObject.DisablePreSelect();

        getObject.Get();
        if (getObject.CommandResult() != Result.Success) return null;

        return getObject.Object(0);
    }

    private Result performMasking(RhinoDoc doc, Guid targetObjectGuid) {
        var objRef = new ObjRef(doc, targetObjectGuid);
        if (objRef == null) return Result.Failure;
        
        var targetBrep = objRef.Brep();
        if (targetBrep == null) {
            RhinoApp.WriteLine("Selected object is not a Brep.");
            return Result.Failure;
        }
        var targetMesh = Mesh.CreateFromBrep(targetBrep, MeshingParameters.Default);
        targetBrep.Flip();

        LoadTilesCommand loadTilesCommand = LoadTilesCommand.Instance;
        if (loadTilesCommand == null) {
            Console.WriteLine("Couldn't find the LoadTilesCommand object. Can't determine intersecting objects.");
            return Result.Failure;
        }

        // if (loadTilesCommand.displayConduit == null) return Result.Failure;

        var importedTiles = TemporaryGeometryConduit.Instance.importedObjects;
        // if (importedTiles == null) return Result.Failure;

        var tilesIntersected = new List<RhinoObject>();
        var trimmings = new List<Brep[]>();

        double tolerance = doc.ModelAbsoluteTolerance; // I don't know what this is. Vibe coding moment.
        foreach (var tile in importedTiles) {
            var geo = tile.Geometry;
            if (geo is Mesh mesh) {
                var meshSet = new Mesh[1];
                meshSet[0] = mesh;
                var meshBrep = Brep.CreateFromMesh(mesh, true);
                if (meshBrep == null) continue;
                Mesh[] intersection = Mesh.CreateBooleanIntersection(targetMesh, meshSet);
                bool inside = false;
                BoundingBox boundingBox = geo.GetBoundingBox(true);

                Point3d[] corners = boundingBox.GetCorners();

                foreach (Point3d corner in corners)
                {
                    bool result = targetBrep.IsPointInside(corner, tolerance, true);
                    if (result != true)
                        inside = true;
                }

                if (intersection.Length > 0 || inside) {
                    tilesIntersected.Add(tile);
                    trimmings.Add(meshBrep.Trim(targetBrep, tolerance));                    
                }
            } else {
                // TODO: will it always be a Mesh? If not, we should probably do something about it.
                Console.WriteLine("Not a Mesh");
            }
        }

        Console.WriteLine("Tiles intersected:");
        Console.WriteLine(tilesIntersected.Count);

        foreach (var tile in tilesIntersected) {
            TemporaryGeometryConduit.Instance.importedObjects.Remove(tile);
        }

        foreach (var newBreps in trimmings) {
            foreach (var brep in newBreps){
                Guid id = doc.Objects.AddBrep(brep);
                RhinoObject obj = doc.Objects.FindId(id);
                /*
                // Set the object's material to a solid colour
                var attributes = obj.Attributes.Duplicate();
                attributes.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
                var material = new Rhino.DocObjects.Material{ DiffuseColor = System.Drawing.Color.Gray };
                int materialIndex = doc.Materials.Add(material);
                attributes.MaterialIndex = materialIndex;
                doc.Objects.ModifyAttributes(obj, attributes, true);
                */
                TemporaryGeometryConduit.Instance.AddObject(obj);
                doc.Objects.Delete(obj);
            }
        }
        
        // TODO: Find out how to re-add materials to the breps that are redrawn or find a way to remove the intersection without converting to brep/mesh

        doc.Views.Redraw();

        return Result.Success;
    }

    public static MaskingCommand Instance { get; set; }
}