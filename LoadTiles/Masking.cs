using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Rhino.Geometry.Intersect;

namespace LoadTiles;

public class MaskingCommand : Command {
    public override string EnglishName => "Mask";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) {
        var getObject = new GetObject();
        getObject.SetCommandPrompt("Select the masking mesh");
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
                }
            } else {
                // TODO: will it always be a Mesh? If not, we should probably do something about it.
                Console.WriteLine("Not a Mesh");
            }
        }

        Console.WriteLine("Tiles intersected:");
        Console.WriteLine(tilesIntersected.Count);

        Mesh[] meshesToUpdate = new Mesh[tilesIntersected.Count];
        for (int i = 0; i < tilesIntersected.Count; i++) {
            var geometry = tilesIntersected[i].Geometry;
            if (geometry is Mesh mesh) {
                meshesToUpdate[i] = mesh;
            } else {
                throw new InvalidOperationException("Expected all geometries to be Meshes, but this isn't the case.");
            }
        }

        var targetBrepMesh = Mesh.CreateFromBrep(targetBrep, MeshingParameters.QualityRenderMesh);
        MeshBooleanOptions options = new() {Tolerance = tolerance};
        Mesh[] newMeshes = Mesh.CreateBooleanDifference(meshesToUpdate, targetBrepMesh, options, out var _);

        foreach (var tile in tilesIntersected) {
            loadTilesCommand.displayConduit.importedObjects.Remove(tile);
        }

        Console.WriteLine("Meshes to add back: " + newMeshes.Length);
        
        for (int i = 0; i < newMeshes.Length; i++) {
            Mesh mesh = newMeshes[i];
            Guid id = doc.Objects.AddMesh(mesh);
            RhinoObject obj = doc.Objects.FindId(id);
            loadTilesCommand.displayConduit.addObject(obj);
            doc.Objects.Delete(obj);
        }

        // TODO: fix the above. It produces weird results. When I tried it on dover.3dm, it creates a few really small random meshes. Maybe someone else knows what to do?

        doc.Views.Redraw();

        return Result.Success;
    }
}