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
        Title = "Cesium ION 3D Tile Import";
        ClientSize = new Size(1200, 640);

        var titleLabel = new Label {
            Text = "Cesium ION 3D Tile Import",
            Font = new Font("Helvetica", 18, FontStyle.Bold)
        };

        var subtitleLabel = new Label {
            Text = "Choose a 3D Tile to import",
            Font = new Font("Helvetica", 10)
        };

        var subtitleLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = subtitleLabel
        };

        var assetsStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Items = { }
        }; 

        foreach (CesiumAsset asset in assets) {
            if (asset.id == null) continue;

            Label nameLabel = new Label {
                Text = asset.name,
                Font = new Font("Helvetica", 18, FontStyle.Bold)
            };

            Label descriptionLabel = null;
            if (asset.description != null) {
                descriptionLabel = new Label {
                    Text = asset.description,
                    Font = new Font("Helvetica", 10)
                };
            }

            Label attributionLabel = null;
            if (asset.attribution != null) {
                attributionLabel = new Label {
                    Text = asset.attribution,
                    Font = new Font("Helvetica", 10)
                };
            }

            Label idLabel = new Label {
                Text = $"ID: {asset.id}",
                Font = new Font("Helvetica", 10, FontStyle.Bold)
            };

            Label dateLabel = null;
            if (asset.dateAdded != null) {
                dateLabel = new Label {
                    Text = $"Date Added: {asset.dateAdded}",
                    Font = new Font("Helvetica", 10, FontStyle.Bold)
                };
            }

            StackLayout metadataStackLayout = new StackLayout {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items = { idLabel, dateLabel }
            };

            Button importButton = new Button{Text = "Import"};
            importButton.Click += (sender, e) => {
                Close(asset);
            };

            var panel = new Panel {
                Padding = new Padding(0, 0, 0, 10),
                Content = new StackLayout {
                    Padding = 30,
                    Spacing = 10,
                    Items = {
                        nameLabel,
                        metadataStackLayout,
                        descriptionLabel,
                        attributionLabel,
                        importButton
                    }
                }
            };

            assetsStackLayout.Items.Add(panel);
        }

        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);

        Content = new StackLayout {
            Padding = 30,
            Spacing = 10,
            Items = {
                titleLabel,
                subtitleLabelPanel,
                new Scrollable {
                    Content = assetsStackLayout,
                    Size = new Size(1140, 500)
                },
                AbortButton
            }
        };
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
    private TextBox apiKeyTextBox;
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
        Title = "GDP Plugin Window";
        ClientSize = new Size(600, 400);
        Resizable = true; // TODO: maybe remove?

        var titleLabel = new Label {
            Text = "GDP Plugin",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 18, FontStyle.Bold)
        };

        var subtitleLabel = new Label {
            Text = "Enter all of the data in this window",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };

        var subtitleLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = subtitleLabel
        };

        var apiKeyLabel = new Label {
            Text = "API key:",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.apiKeyTextBox = new TextBox{Width = 200};

        this.selectedAsset = this.getDefaultSelectedAsset(); // TODO: save this information with the other .3dm data

        var modelLabel = new Label {
            Text = "Model:",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.selectedModelLabel = new Label {
            Text = this.selectedAsset.name,
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10, FontStyle.Bold)
        };
        var changeModelButton = new Button{Text = "Change"};
        changeModelButton.Click += (sender, e) => {
            this.selectNewModel();
        };

        var latitudeLabel = new Label {
            Text = "Latitude:",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.latitudeTextBox = new TextBox();
        var longitudeLabel = new Label {
            Text = "Longitude:",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.longitudeTextBox = new TextBox();

        var altitudeLabel = new Label {
            Text = "Altitude (what are the units? I'm guessing metres?):",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.altitudeTextBox = new TextBox();
        this.altitudeTextBox.Text = "0";

        var radiusLabel = new Label {
            Text = "Radius (what are the units? I'm guessing metres?)",
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };
        this.radiusTextBox = new TextBox();
        this.radiusTextBox.Text = "200";

        // All Auth stuff :
        // Display for 'Logged In' / 'Logged Out' status
        var loggedInLabel = new Label {
            Text = (AuthSession.IsLoggedIn ? "Logged in with key: " + AuthSession.CesiumAccessToken : "You are not logged in."),
            VerticalAlignment = VerticalAlignment.Center,
            Font = new Font("Helvetica", 10)
        };

        // Button authButton = new Button{Text = "Click here to do auth"};
        this.authButton = new Button{Text = (AuthSession.IsLoggedIn ? "Log Out" : "Log In")};
        this.authButton.Click += (sender, e) => {
            if (AuthSession.IsLoggedIn)
            {
                // Log out the user
                AuthSession.Logout();
                MessageBox.Show("Logging out...");
                authButton.Text = "Log In";
                loggedInLabel.Text = "You are not logged in.";
                return;
            }

            string? key = AuthSession.Login();

            if (AuthSession.IsLoggedIn)
            {
                MessageBox.Show("Authentication successful!");
                authButton.Text = "Log Out";
                loggedInLabel.Text = "Logged in with key: " + key;
            }
            else
            {
                MessageBox.Show("Authentication failed. Please try again.");
            }
        };


        DefaultButton = new Button{Text = "Submit"};
        DefaultButton.Click += (sender, e) => {
            DialogResult result = this.getUserInput();
            if (result == null) return;
            Close(result);
        };

        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);

        TableLayout apiKeyTableLayout = new TableLayout {
            Padding = 10,
            Spacing = new Size(10, 10),
            Rows = {
                new TableRow(new TableCell(apiKeyLabel, false), new TableCell(apiKeyTextBox, true))
            }
        };
        
        StackLayout authStackLayout = new StackLayout {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Items = { null, loggedInLabel, this.authButton }
        };

        StackLayout modelStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = { null, modelLabel, this.selectedModelLabel, changeModelButton }
        };

        StackLayout latitudeStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = { null, latitudeLabel, this.latitudeTextBox }
        };

        StackLayout longitudeStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = { null, longitudeLabel, this.longitudeTextBox }
        };

        StackLayout altitudeStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = { null, altitudeLabel, this.altitudeTextBox }
        };

        StackLayout radiusStackLayout = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = { null, radiusLabel, this.radiusTextBox }
        };

        StackLayout buttons = new StackLayout {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Items = { null, DefaultButton, AbortButton }
        };

        Content = new StackLayout {
            Padding = 30,
            Spacing = 10,
            Items = {
                titleLabel,
                subtitleLabelPanel,
                authStackLayout,
                modelStackLayout,
                latitudeStackLayout,
                longitudeStackLayout,
                altitudeStackLayout,
                radiusStackLayout,
                new StackLayoutItem(null, expand: true),
                buttons
            }
        };
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