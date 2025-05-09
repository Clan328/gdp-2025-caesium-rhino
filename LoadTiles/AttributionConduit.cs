using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.UI;

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
    private AttributionMouseCallback mouseCallback;
    public AttributionConduit() {
        this.Enabled = true;
        this.mouseCallback = new AttributionMouseCallback();
        this.mouseCallback.Enabled = true;
    }

    public void setClickURL(string url) {
        this.mouseCallback.url = url;
    }

    public async void loadGoogleImage() {
        string url = "https://www.google.co.uk/images/branding/googlelogo/1x/googlelogo_white_background_color_272x92dp.png";

        using (HttpClient client = new HttpClient()) {
            try {
                byte[] imageBytes = await client.GetByteArrayAsync(url);
                using (MemoryStream ms = new MemoryStream(imageBytes)) {
                    var bitmap = new Bitmap(ms);
                    this.logoImage = new DisplayBitmap(bitmap);
                }
            } catch (Exception e) {
                Console.WriteLine($"Error downloading image: {e.Message}");
                return;
            }
        }
    }

    public void removeImage() {
        if (this.logoImage == null) return;
        
        this.logoImage.Dispose();
        this.logoImage = null;
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

        this.mouseCallback.minX = backgroundFilledRectangle.X;
        this.mouseCallback.maxX = backgroundFilledRectangle.X + backgroundFilledRectangle.Width;
        this.mouseCallback.minY = backgroundFilledRectangle.Y;
        this.mouseCallback.maxY = backgroundFilledRectangle.Y + backgroundFilledRectangle.Height;
    }

    private void drawLogo(DrawEventArgs e, int fontSize, int padding) {
        if (this.logoImage == null) return;

        var logoSize = this.logoImage.Size;
        var logoAspectRatio = (float) logoSize.Width / logoSize.Height;

        int logoHeight = fontSize;
        int logoWidth = (int) (logoAspectRatio * logoHeight);

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

public class AttributionMouseCallback : Rhino.UI.MouseCallback {
    public int minX = 0;
    public int maxX = 0;
    public int minY = 0;
    public int maxY = 0;
    public string url = "";
    protected override void OnMouseDown(MouseCallbackEventArgs e)
    {
        base.OnMouseDown(e);

        var p = e.ViewportPoint;
        if (p.X >= minX && p.X <= maxX) {
            if (p.Y >= minY && p.Y <= maxY) {
                if (url != "") {
                    Process.Start(new ProcessStartInfo {
                        FileName = this.url,
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}