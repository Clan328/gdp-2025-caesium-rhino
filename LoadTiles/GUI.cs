using System;
using Rhino;
using Rhino.Commands;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using System.Collections.Generic;
using System.Text.Json;
using CesiumAuthentication;

namespace LoadTiles;

public class Styling {
    public static Color colourVeryLight = Color.FromRgb(0xC9F2C7);
    public static Color colourLighter = Color.FromRgb(0xACECA1);
    public static Color colourLight = Color.FromRgb(0x96BE8C);
    public static Color colourDark = Color.FromRgb(0x629460);
    public static Color colourDarker = Color.FromRgb(0x243119);
    public static string fontName = "Helvetica";

    public static Panel createHeaderPanel(string title, string subtitle) {
        var headerPanel = new Panel {
            BackgroundColor = colourLight,
            Padding = 20,
            Width = 100000 // Surely there is a better way of doing this.
        };

        var titleLabel = label(title, 18, true);
        var subtitleLabel = label(subtitle, 10);

        headerPanel.Content = new StackLayout {
            Orientation = Orientation.Vertical,
            Items = {
                titleLabel,
                subtitleLabel
            }
        };

        return headerPanel;
    }

    public static Label label(string text, int fontSize, bool bold = false) {
        return new Label{
            Text = text,
            Font = new Font(Styling.fontName, fontSize, bold ? FontStyle.Bold : FontStyle.None)
        };
    }
}

public record class CesiumAsset (
    int? id,
    string name,
    string? description,
    string? attribution,
    string type,
    int? bytes,
    DateTimeOffset? dateAdded,
    string? status,
    int? percentComplete,
    bool? archivable,
    bool? exportable
);

public record class CesiumAssets (
    List<CesiumAsset> items
)
{
    public readonly static JsonSerializerOptions JsonSerializerOptions
    = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true
    };

    public static List<CesiumAsset> FromJson(string data) {
        return JsonSerializer.Deserialize<CesiumAssets>(data, JsonSerializerOptions).items;
    }
}

public class CesiumImportDialog : Dialog<CesiumAsset?> {  
    public CesiumImportDialog(List<CesiumAsset> assets) {
        Title = "Cesium ion assets";
        ClientSize = new Size(800, 600);
        Resizable = true;

        Content = createDialogContent(assets);
    }

    private DynamicLayout createDialogContent(List<CesiumAsset> assets) {
        var headerPanel = Styling.createHeaderPanel(
            "Cesium ion assets",
            "These are the assets that you have access to with your Cesium ion account. Select which one you'd like to import."
        );
        var assetsPanel = createAssetsPanel(assets);
        var buttonDynamicLayout = createButtonPanel();

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        dynamicLayout.Add(headerPanel, true);
        dynamicLayout.Add(assetsPanel, true, true);
        dynamicLayout.Add(buttonDynamicLayout);
        dynamicLayout.EndVertical();

        return dynamicLayout;
    }

    private Panel createAssetsPanel(List<CesiumAsset> assets) {
        var assetsDynamicLayout = new DynamicLayout {
            Padding = 15,
            Height = -1
        };
        assetsDynamicLayout.BeginVertical();

        foreach (CesiumAsset asset in assets) {
            if (asset.id == null) continue;

            var assetPanel = createAssetPanel(asset);

            assetsDynamicLayout.Add(assetPanel, true, false);
        }

        assetsDynamicLayout.Add(null, false, false); // TODO: why is there random blank space at the bottom of the Scrollable?

        assetsDynamicLayout.EndVertical();

        var assetsScrollable = new Scrollable {
            BackgroundColor = Styling.colourDark,
            Border = BorderType.None,
            Content = assetsDynamicLayout
        };
        var assetsPanel = new Panel {
            Padding = new Padding(20, 20, 20, 0),
            Content = assetsScrollable
        };

        return assetsPanel;
    }

    private Panel createAssetPanel(CesiumAsset asset) {
        var nameLabel = Styling.label(asset.name, 16, true);

        var descriptionText = asset.description == null ? "No asset description provided." : asset.description;
        var descriptionFontStyle = asset.description == null ? FontStyle.Italic : FontStyle.None;
        var descriptionLabel = new Label {
            Text = descriptionText,
            Font = new Font(Styling.fontName, 11, descriptionFontStyle)
        };

        var attributionText = asset.attribution == null ? "No asset attribution provided." : asset.attribution;
        var attributionFontStyle = asset.attribution == null ? FontStyle.Italic : FontStyle.None;
        var attributionLabel = new Label {
            Text = attributionText,
            Font = new Font(Styling.fontName, 9, attributionFontStyle)
        };
        var attributionLabelPanel = new Panel {
            Padding = new Padding(0, 10, 0, 0),
            Content = attributionLabel
        };

        var idLabel = Styling.label($"ID: {asset.id}", 10, true);

        var dateText = asset.dateAdded == null ? "Date added: unknown" : $"Date added: {asset.dateAdded}";
        var dateLabel = Styling.label(dateText, 10, true);

        StackLayout metadataStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 30,
            Padding = new Padding(0, 0, 0, 20),
            Items = { idLabel, dateLabel }
        };

        Button importButton = new Button{Text = "Select"};
        importButton.Click += (sender, e) => {
            Close(asset);
        };

        var importButtonDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 10, 0, 0)
        };
        importButtonDynamicLayout.BeginHorizontal();
        importButtonDynamicLayout.Add(null, true);
        importButtonDynamicLayout.Add(importButton);
        importButtonDynamicLayout.Add(null, true);
        importButtonDynamicLayout.EndHorizontal();

        var assetDynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourLighter,
            Padding = 10,
            Width = 0
        };
        assetDynamicLayout.BeginVertical();
        assetDynamicLayout.Add(nameLabel);
        assetDynamicLayout.Add(metadataStackLayout);
        assetDynamicLayout.Add(descriptionLabel);
        assetDynamicLayout.Add(attributionLabelPanel);
        assetDynamicLayout.Add(importButtonDynamicLayout);
        assetDynamicLayout.EndVertical();

        var assetPanel = new Panel {
            Padding = new Padding(0, 0, 0, 15),
            Content = assetDynamicLayout
        };

        return assetPanel;
    }

    private DynamicLayout createButtonPanel() {
        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);

        var buttonPanel = new Panel {
            BackgroundColor = Styling.colourLight,
            Padding = 10,
            Content = AbortButton
        };

        var buttonDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 30, 0, 10)
        };
        buttonDynamicLayout.BeginHorizontal();
        buttonDynamicLayout.Add(null, true);
        buttonDynamicLayout.Add(buttonPanel);
        buttonDynamicLayout.Add(null, true);
        buttonDynamicLayout.EndHorizontal();

        return buttonDynamicLayout;
    }
}

public class DialogResult {
    public string apiKey;
    public CesiumAsset selectedAsset;
    public double latitude;
    public double longitude;
    public double altitude;
    public double radius;
    public DialogResult(string apiKey, CesiumAsset selectedAsset, double latitude, double longitude, double altitude, double radius) {
        this.apiKey = apiKey;
        this.selectedAsset = selectedAsset;
        this.latitude = latitude;
        this.longitude = longitude;
        this.altitude = altitude;
        this.radius = radius;
    }
}

public class GDPDialog : Dialog<DialogResult> {
    private TextBox latitudeTextBox;
    private TextBox longitudeTextBox;
    private TextBox altitudeTextBox;
    private TextBox radiusTextBox;
    private Button authButton;

    private CesiumAsset selectedAsset;
    private Label selectedModelLabel;
    private static readonly HttpClient client = new HttpClient();

    private CesiumAsset getDefaultSelectedAsset() {
        return new CesiumAsset(
            2275207,
            "Google Photorealistic 3D Tiles",
            null, null, "", null, null, null, null, null, null
        );
    }

    public GDPDialog() {
        Title = "Fetch real world data";
        ClientSize = new Size(400, 550);

        this.selectedAsset = this.getDefaultSelectedAsset();

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Fetch data",
            "What data do you want to import?"
        );
        var authDynamicLayout = createAuthenticationPanel();
        var modelDynamicLayout = createModelPanel();
        var positionDynamicLayout = createPositionPanel();
        var buttonsDynamicLayout = createButtonsPanel();

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        dynamicLayout.Add(headerPanel, true);
        dynamicLayout.Add(authDynamicLayout, true);
        dynamicLayout.Add(modelDynamicLayout, true);
        dynamicLayout.Add(positionDynamicLayout, true);
        dynamicLayout.Add(buttonsDynamicLayout, true);
        dynamicLayout.Add(null, true);
        dynamicLayout.EndVertical();

        return dynamicLayout;
    }

    private DynamicLayout createAuthenticationPanel() {
        var authLabel = Styling.label("Authentication", 12);

        string loggedInText = "You are logged in.";
        string loggedOutText = "You are logged out.";

        var loggedInLabel = Styling.label(AuthSession.IsLoggedIn ? loggedInText : loggedOutText, 10);
        var loggedInLabelPanel = new Panel {
            Padding = new Padding(0, 0, 30, 0),
            Content = loggedInLabel
        };

        this.authButton = new Button{Text = AuthSession.IsLoggedIn ? "Log out" : "Log in"};
        this.authButton.Click += (sender, e) => {
            if (AuthSession.IsLoggedIn)
            {
                // Log out the user
                AuthSession.Logout();
                MessageBox.Show("Logging out...");
                authButton.Text = "Log in";
                loggedInLabel.Text = loggedOutText;
                return;
            }

            string? key = AuthSession.Login();

            if (AuthSession.IsLoggedIn)
            {
                MessageBox.Show("Authentication successful!");
                authButton.Text = "Log out";
                loggedInLabel.Text = loggedInText;
            }
            else
            {
                MessageBox.Show("Authentication failed. Please try again.");
            }
        };

        var loggedInStatusDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 15, 0, 0)
        };
        loggedInStatusDynamicLayout.BeginHorizontal();
        loggedInStatusDynamicLayout.Add(loggedInLabelPanel);
        loggedInStatusDynamicLayout.Add(null, true);
        loggedInStatusDynamicLayout.Add(this.authButton);
        loggedInStatusDynamicLayout.EndHorizontal();
        
        var authDynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        authDynamicLayoutInner.BeginVertical();
        authDynamicLayoutInner.Add(authLabel);
        authDynamicLayoutInner.Add(loggedInStatusDynamicLayout);
        authDynamicLayoutInner.EndVertical();
        var authDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        authDynamicLayout.BeginHorizontal();
        authDynamicLayout.Add(authDynamicLayoutInner);
        authDynamicLayout.EndHorizontal();

        return authDynamicLayout;
    }

    private DynamicLayout createModelPanel() {
        var modelLabel = Styling.label("Model", 12);

        this.selectedModelLabel = Styling.label(this.selectedAsset.name, 10, true);
        var selectedModelLabelPanel = new Panel {
            Padding = new Padding(0, 0, 20, 0),
            Content = this.selectedModelLabel
        };

        var changeModelButton = new Button{Text = "Change"};
        changeModelButton.Click += (sender, e) => {
            this.selectNewModel();
        };

        var currentModelDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 15, 0, 0)
        };
        currentModelDynamicLayout.BeginHorizontal();
        currentModelDynamicLayout.Add(selectedModelLabelPanel);
        currentModelDynamicLayout.Add(null, true);
        currentModelDynamicLayout.Add(changeModelButton);
        currentModelDynamicLayout.EndHorizontal();

        var modelDynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        modelDynamicLayoutInner.BeginVertical();
        modelDynamicLayoutInner.Add(modelLabel);
        modelDynamicLayoutInner.Add(currentModelDynamicLayout);
        modelDynamicLayoutInner.EndVertical();
        var modelDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        modelDynamicLayout.BeginHorizontal();
        modelDynamicLayout.Add(modelDynamicLayoutInner);
        modelDynamicLayout.EndHorizontal();

        return modelDynamicLayout;
    }

    private DynamicLayout createPositionPanel() {
        var positionLabel = Styling.label("Position", 12);

        var latitudeLabel = Styling.label("Latitude", 10);
        this.latitudeTextBox = new TextBox();
        var latitudeStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Items = { latitudeLabel, this.latitudeTextBox }
        };

        var longitudeLabel = Styling.label("Longitude", 10);
        this.longitudeTextBox = new TextBox();
        var longitudeStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Items = { longitudeLabel, this.longitudeTextBox }
        };

        var altitudeLabel = Styling.label("Altitude", 10);
        this.altitudeTextBox = new TextBox();
        this.altitudeTextBox.Text = "0";
        var altitudeTextBoxLabel = Styling.label("  metres", 9);
        var altitudeTextBoxStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Items = { this.altitudeTextBox, new StackLayoutItem(altitudeTextBoxLabel, VerticalAlignment.Center) }
        };
        var altitudeStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Items = { altitudeLabel, altitudeTextBoxStackLayout }
        };

        var radiusLabel = Styling.label("Radius", 10);
        this.radiusTextBox = new TextBox();
        this.radiusTextBox.Text = "200";
        var radiusTextBoxLabel = Styling.label("  metres", 9);
        var radiusTextBoxStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Items = { this.radiusTextBox, new StackLayoutItem(radiusTextBoxLabel, VerticalAlignment.Center) }
        };
        var radiusStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Items = { radiusLabel, radiusTextBoxStackLayout }
        };

        TableLayout positionTableLayout = new TableLayout {
            Spacing = new Size(20, 20),
            Padding = new Padding(0, 20, 0, 0),
            Rows = {
                new TableRow(latitudeStackLayout, longitudeStackLayout),
                new TableRow(altitudeStackLayout, radiusStackLayout)
            }
        };

        StackLayout positionStackLayoutInner = new StackLayout {
            Orientation = Orientation.Vertical,
            BackgroundColor = Styling.colourDark,
            Padding = 10,
            Items = { positionLabel, positionTableLayout }
        };
        var positionDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        positionDynamicLayout.BeginHorizontal();
        positionDynamicLayout.Add(positionStackLayoutInner);
        positionDynamicLayout.EndHorizontal();

        return positionDynamicLayout;
    }

    private DynamicLayout createButtonsPanel() {
        DefaultButton = new Button{Text = "Import"};
        DefaultButton.Click += (sender, e) => {
            DialogResult result = this.getUserInput();
            if (result == null) return;
            Close(result);
        };
        var defaultButtonPanel = new Panel {
            Padding = new Padding(0, 0, 20, 0),
            Content = DefaultButton
        };

        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);

        StackLayout buttonsStackLayoutInner = new StackLayout {
            Orientation = Orientation.Horizontal,
            BackgroundColor = Styling.colourLight,
            Padding = 10,
            Items = { defaultButtonPanel, AbortButton }
        };
        var buttonsDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 30, 20, 0)
        };
        buttonsDynamicLayout.BeginHorizontal();
        buttonsDynamicLayout.Add(null, true);
        buttonsDynamicLayout.Add(buttonsStackLayoutInner);
        buttonsDynamicLayout.Add(null, true);
        buttonsDynamicLayout.EndHorizontal();

        return buttonsDynamicLayout;
    }

    private void selectNewModel() {
        string? key = AuthSession.Login();
        if (!AuthSession.IsLoggedIn) return;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.CesiumAccessToken);
        string json = Task.Run(() => client.GetStringAsync("https://api.cesium.com/v1/assets?type=3DTILES").Result).GetAwaiter().GetResult();
        List<CesiumAsset> assets = CesiumAssets.FromJson(json);

        CesiumImportDialog dialog = new CesiumImportDialog(assets);
        CesiumAsset asset = dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

        if (asset != null) {
            this.selectedAsset = asset;
            this.selectedModelLabel.Text = asset.name;
        }
    }

    public void prefillData(double latitude, double longitude, double altitude, double radius, CesiumAsset selectedAsset) {
        this.latitudeTextBox.Text = latitude.ToString();
        this.longitudeTextBox.Text = longitude.ToString();
        this.altitudeTextBox.Text = altitude.ToString();
        this.radiusTextBox.Text = radius.ToString();
        this.selectedAsset = selectedAsset;
        this.selectedModelLabel.Text = selectedAsset.name;
    }

    private DialogResult getUserInput() {
        string apiKey = AuthSession.CesiumAccessToken;
        string latitudeText = this.latitudeTextBox.Text;
        string longitudeText = this.longitudeTextBox.Text;
        string altitudeText = this.altitudeTextBox.Text;
        string radiusText = this.radiusTextBox.Text;
        bool canConvertLatitude = double.TryParse(latitudeText, out double latitude);
        bool canConvertLongitude = double.TryParse(longitudeText, out double longitude);
        bool canConvertAltitude = double.TryParse(altitudeText, out double altitude);
        bool canConvertRadius = double.TryParse(radiusText, out double radius);
        bool latitudeValid = canConvertLatitude && latitude >= -90 && latitude <= 90;
        bool longitudeValid = canConvertLongitude && longitude >= -180 && longitude <= 180;
        if (string.IsNullOrWhiteSpace(apiKey)) {
            MessageBox.Show("You are not logged in!", "Error", MessageBoxType.Error);
            return null;
        } 

        if (!latitudeValid || !longitudeValid) {
            MessageBox.Show("You have entered invalid coordinate values.", "Error", MessageBoxType.Error);
            if (!latitudeValid) {
                this.latitudeTextBox.BackgroundColor = Colors.DarkRed;
                this.latitudeTextBox.TextColor = Colors.White;
            } else {
                this.latitudeTextBox.BackgroundColor = Colors.White;
                this.latitudeTextBox.TextColor = Colors.Black;
            }
            if (!longitudeValid) {
                this.longitudeTextBox.BackgroundColor = Colors.DarkRed;
                this.longitudeTextBox.TextColor = Colors.White;
            } else {
                this.longitudeTextBox.BackgroundColor = Colors.White;
                this.longitudeTextBox.TextColor = Colors.Black;
            }
            return null;
        } else {
            this.latitudeTextBox.BackgroundColor = Colors.White;
            this.longitudeTextBox.BackgroundColor = Colors.White;
            this.latitudeTextBox.TextColor = Colors.Black;
            this.longitudeTextBox.TextColor = Colors.Black;
        }

        if (!canConvertAltitude) {
            MessageBox.Show("You have entered an invalid altitude value.", "Error", MessageBoxType.Error);
            this.altitudeTextBox.BackgroundColor = Colors.DarkRed;
            this.altitudeTextBox.TextColor = Colors.White;
        } else {
            this.altitudeTextBox.BackgroundColor = Colors.White;
            this.altitudeTextBox.TextColor = Colors.Black;
        }

        if (!canConvertRadius) {
            MessageBox.Show("You have entered an invalid radius value.", "Error", MessageBoxType.Error);
            this.radiusTextBox.BackgroundColor = Colors.DarkRed;
            this.radiusTextBox.TextColor = Colors.White;
        } else {
            this.radiusTextBox.BackgroundColor = Colors.White;
            this.radiusTextBox.TextColor = Colors.Black;
        }

        RhinoApp.WriteLine("Fetch Successful! \n" +
            "Model Name: " + this.selectedAsset.name + "\n" +
            "Latitude: " + latitude.ToString() + "\n" +
            "Longitude: " + longitude.ToString() + "\n" +
            "Altitude: " + altitude.ToString() + "\n" +
            "Radius: " + radius.ToString()
        );

        return new DialogResult(
            apiKey,
            this.selectedAsset,
            latitude,
            longitude,
            altitude,
            radius
        );
    }
}