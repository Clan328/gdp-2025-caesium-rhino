using System;
using Rhino;
using Rhino.Geometry;
using Rhino.Display;
using Rhino.DocObjects;
using System.Collections.Generic;
using Rhino.Render.CustomRenderMeshes;

namespace LoadTiles;

public class TemporaryGeometryConduit : DisplayConduit {
    public List<RhinoObject> importedObjects = new List<RhinoObject>();

    private static TemporaryGeometryConduit instance = null;

    private TemporaryGeometryConduit() {
        this.importedObjects = new List<RhinoObject>();
        this.Enabled = true;
    }

    public static TemporaryGeometryConduit Instance {
        get {
            if (instance == null) {
                instance = new TemporaryGeometryConduit();
            }
            return instance;
        }
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

    public void AddObject(RhinoObject obj) {
        this.importedObjects.Add(obj);
    }

    public void Reset() {
        importedObjects = new List<RhinoObject>();
    }
}