using System;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace LoadTiles;

public class AttributionConduit : DisplayConduit {
    private static AttributionConduit instance = null;

    public static AttributionConduit Instance {
        get {
            if (instance == null) {
                instance = new AttributionConduit();
            }
            return instance;
        }
    }

    private string attributionText = "";
    private DisplayBitmap? googleLogo;
    public AttributionConduit() {
        this.Enabled = true;
        
        // var googleBitmap = new Bitmap("C:\\Users\\dylan\\Downloads\\google_logos\\google_on_white.png");
        // this.googleLogo = new DisplayBitmap(googleBitmap);
        // TODO: load Google logo on the fly
    }

    public void setAttributionText(string attributionText) {
        this.attributionText = attributionText;
    }

    public string getAttributionText() {
        return this.attributionText;
    }

    protected override void PostDrawObjects(DrawEventArgs e) {
        base.PostDrawObjects(e);

        if (attributionText == "") return;
        if (this.googleLogo == null) return;

        var logoSize = this.googleLogo.Size;
        var logoAspectRatio = logoSize.Width / logoSize.Height;

        int fontSize = 12;
        int padding = 3;
        int logoHeight = fontSize;
        int logoWidth = logoAspectRatio * logoHeight;

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
            textX - padding - logoWidth, textY - padding,
            textWidth + padding * 2 + logoWidth, textHeight + padding * 2
        );
        e.Display.Draw2dRectangle(
            backgroundFilledRectangle,
            System.Drawing.Color.Transparent,
            0,
            System.Drawing.Color.White
        );

        e.Display.DrawSprite(
            this.googleLogo,
            new Point2d(textX - logoWidth / 2, textY + logoHeight / 2),
            logoWidth,
            logoHeight
        );

        e.Display.Draw2dText(
            this.attributionText,
            System.Drawing.Color.Black,
            new Point2d(textX, textY),
            false, fontSize, Styling.fontName
        );
    }
}