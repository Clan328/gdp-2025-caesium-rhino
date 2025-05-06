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
    private DisplayBitmap? logoImage;
    public AttributionConduit() {
        this.Enabled = true;
        
        // var googleBitmap = new Bitmap("C:\\Users\\dylan\\Downloads\\google_logos\\google_on_white.png");
        // this.logoImage = new DisplayBitmap(googleBitmap);
        // TODO: load Google logo on the fly
    }

    public void setAttributionText(string attributionText) {
        this.attributionText = attributionText;
    }

    public string getAttributionText() {
        return this.attributionText;
    }

    private void drawAttributionText(DrawEventArgs e, int fontSize, int padding) {
        if (this.attributionText == "") return;

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

    private void drawLogo(DrawEventArgs e, int fontSize, int padding) {
        if (this.logoImage == null) return;

        var logoSize = this.logoImage.Size;
        var logoAspectRatio = logoSize.Width / logoSize.Height;

        int logoHeight = fontSize;
        int logoWidth = logoAspectRatio * logoHeight;

        var viewportRectangle = e.Viewport.Size;
        var imageY = viewportRectangle.Height - logoHeight - padding;
        var backgroundFilledRectangle = new Rectangle(
            0, imageY - padding,
            logoWidth + padding * 2, logoHeight + padding * 2
        );
        e.Display.Draw2dRectangle(
            backgroundFilledRectangle,
            System.Drawing.Color.Transparent,
            0,
            System.Drawing.Color.White
        );
        e.Display.DrawSprite(
            this.logoImage,
            new Point2d((logoWidth + padding) / 2, imageY + logoHeight / 2),
            logoWidth,
            logoHeight
        );
    }

    protected override void PostDrawObjects(DrawEventArgs e) {
        base.PostDrawObjects(e);

        int fontSize = 12;
        int padding = 3;

        this.drawAttributionText(e, fontSize, padding);
        this.drawLogo(e, fontSize, padding);
    }
}