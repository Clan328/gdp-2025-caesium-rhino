using System;
using Rhino;
using Rhino.Geometry;
using Rhino.Display;
using Rhino.DocObjects;
using System.Collections.Generic;

namespace LoadTiles;

public class TemporaryGeometryConduit : DisplayConduit {
    private List<RhinoObject> importedObjects;

    public TemporaryGeometryConduit() {
        this.importedObjects = new List<RhinoObject>();
        this.Enabled = true;
    }

    /// <summary>
    /// This function is called to determine the bounding box of our DisplayConduit.
    /// If we don't set it properly, it will clip off the screen sometimes, even when it should otherwise be visible.
    /// </summary>
    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e) {
        base.CalculateBoundingBox(e);
        var bbox = new BoundingBox();
        bbox.Union(e.Display.Viewport.ConstructionPlane().Origin);
        foreach (var obj in this.importedObjects) {
            bbox.Union(obj.Geometry.GetBoundingBox(e.Display.Viewport.ConstructionPlane()));
        }
        e.IncludeBoundingBox(bbox);
    }

    // We use the function PreDrawObjects so that our imported objects are drawn behind the objects created by the user.
    protected override void PreDrawObjects(DrawEventArgs e) {
        foreach (var obj in this.importedObjects) {
            e.Display.DrawObject(obj);
        }
    }

    public void addObject(RhinoObject obj) {
        this.importedObjects.Add(obj);
    }
}