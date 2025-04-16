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

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e) {
        base.CalculateBoundingBox(e);
        var bbox = new BoundingBox();
        bbox.Union(e.Display.Viewport.ConstructionPlane().Origin);
        foreach (var obj in this.importedObjects) {
            bbox.Union(obj.Geometry.GetBoundingBox(e.Display.Viewport.ConstructionPlane()));
        }
        e.IncludeBoundingBox(bbox);
    }

    protected override void PreDrawObjects(DrawEventArgs e) {
        foreach (var obj in this.importedObjects) {
            e.Display.DrawObject(obj);
        }
    }

    public void addObject(RhinoObject obj) {
        this.importedObjects.Add(obj);
    }
}