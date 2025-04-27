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
    public override string EnglishName => "Mask";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) {
        var getObject = new GetObject();
        getObject.SetCommandPrompt("Select the masking object");
        getObject.GeometryFilter = ObjectType.Brep;
        getObject.SubObjectSelect = false;
        getObject.DeselectAllBeforePostSelect = true;
        getObject.DisablePreSelect();

        getObject.Get();
        if (getObject.CommandResult() != Result.Success) return getObject.CommandResult();

        var objRef = getObject.Object(0);
        if (objRef == null) return Result.Failure;

        var targetBrep = objRef.Brep();
        if (targetBrep == null) {
            RhinoApp.WriteLine("Selected object is not a Brep.");
            return Result.Failure;
        }
        targetBrep.Flip();

        Command[] commands = PlugIn.GetCommands();
        LoadTilesCommand loadTilesCommand = null;
        foreach (Command command in commands) {
            if (command.EnglishName == "Fetch") {
                loadTilesCommand = (LoadTilesCommand) command;
            }
        }
        if (loadTilesCommand == null) {
            Console.WriteLine("Couldn't find the LoadTilesCommand object. Can't determine intersecting objects.");
            return Result.Failure;
        }

        if (loadTilesCommand.displayConduit == null) return Result.Failure;

        var importedTiles = loadTilesCommand.displayConduit.importedObjects;
        if (importedTiles == null) return Result.Failure;

        var tilesIntersected = new List<RhinoObject>();
        var trimmings = new List<Brep[]>();

        double tolerance = doc.ModelAbsoluteTolerance; // I don't know what this is. Vibe coding moment.
        foreach (var tile in importedTiles) {
            var geo = tile.Geometry;
            if (geo is Mesh mesh) {
                var meshBrep = Brep.CreateFromMesh(mesh, true);
                if (meshBrep == null) continue;
                
                Intersection.BrepBrep(targetBrep, meshBrep, tolerance, out Curve[] curves, out Point3d[] points);
                if (curves.Length > 0 || points.Length > 0) {
                    // TODO: is this the best way of checking this? I have no idea.
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
            loadTilesCommand.displayConduit.importedObjects.Remove(tile);
        }

        foreach (var newBreps in trimmings) {
            foreach (var brep in newBreps){
                Guid id = doc.Objects.AddBrep(brep);
                RhinoObject obj = doc.Objects.FindId(id);

                // Set the object's material to a solid colour
                var attributes = obj.Attributes.Duplicate();
                attributes.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
                var material = new Rhino.DocObjects.Material{ DiffuseColor = System.Drawing.Color.Gray };
                int materialIndex = doc.Materials.Add(material);
                attributes.MaterialIndex = materialIndex;
                doc.Objects.ModifyAttributes(obj, attributes, true);

                loadTilesCommand.displayConduit.addObject(obj);
                doc.Objects.Delete(obj);
            }
        }
        
        // TODO: Find out how to re-add materials to the breps that are redrawn or find a way to remove the intersection without converting to brep/mesh

        doc.Views.Redraw();

        return Result.Success;
    }
}