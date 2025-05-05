using System;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace LoadTiles;

public class AttributionConduit : DisplayConduit {
    private string attributionText = "Hello world";
    public AttributionConduit() {
        this.Enabled = true;
    }

    public void setAttributionText(string attributionText) {
        this.attributionText = attributionText;
    }

    public string getAttributionText() {
        return this.attributionText;
    }

    protected override void PostDrawObjects(DrawEventArgs e) {
        base.PostDrawObjects(e);
        var textBoundsRectangle = e.Display.Measure2dText(
            this.attributionText,
            new Point2d(0, 0),
            false,
            0,
            9,
            Styling.fontName
        );
        var viewportRectangle = e.Viewport.Size;
        var textX = viewportRectangle.Width - textBoundsRectangle.Width;
        var textY = viewportRectangle.Height;
        Console.WriteLine("Screen coordinates");
        Console.WriteLine(textX);
        Console.WriteLine(textY);
        var backgroundFilledRectangle = new Rectangle(
            textX, textY - textBoundsRectangle.Height,
            textBoundsRectangle.Width, textBoundsRectangle.Height
        );
        e.Display.Draw2dRectangle(
            backgroundFilledRectangle,
            System.Drawing.Color.Transparent,
            0,
            System.Drawing.Color.White
        );
        e.Display.Draw2dText(
            this.attributionText,
            System.Drawing.Color.Black,
            new Point2d(textX, textY),
            false, 9, Styling.fontName
        );
    }
}