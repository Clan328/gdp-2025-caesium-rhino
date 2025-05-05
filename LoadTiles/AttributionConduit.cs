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

        int fontSize = 12;
        int padding = 3;
        var textBoundsRectangle = e.Display.Measure2dText(
            this.attributionText,
            new Point2d(0, 0),
            false,
            0,
            fontSize,
            Styling.fontName
        );
        var viewportRectangle = e.Viewport.Size;
        int textWidth = Math.Abs(textBoundsRectangle.Width);
        int textHeight = Math.Abs(textBoundsRectangle.Height);
        var textX = viewportRectangle.Width - textWidth - padding;
        var textY = viewportRectangle.Height - textHeight - padding;
        var backgroundFilledRectangle = new Rectangle(
            textX - padding, textY - padding,
            textWidth + padding * 2, textHeight + padding * 2
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
            false, fontSize, Styling.fontName
        );
    }
}