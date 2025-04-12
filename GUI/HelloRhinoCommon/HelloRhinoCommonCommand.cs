using Rhino;
using Rhino.Commands;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using CesiumAuth;

namespace HelloRhinoCommon
{
    public class HelloRhinoCommonCommand : Rhino.Commands.Command
    {
        public HelloRhinoCommonCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static HelloRhinoCommonCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "MyCommand";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode) {
            RhinoApp.WriteLine("Command is running.");
            MyDialog dialog = new MyDialog();
            DialogResult result = dialog.ShowModal(RhinoEtoApp.MainWindow);

            if (result == null) {
                RhinoApp.WriteLine("User pressed Cancel.");
            } else {
                RhinoApp.WriteLine(result.apiKey);
                RhinoApp.WriteLine(result.modelName);
                RhinoApp.WriteLine(result.latitude.ToString());
                RhinoApp.WriteLine(result.longitude.ToString());
            }

            RhinoApp.WriteLine("Dialog has been closed.");
            return Rhino.Commands.Result.Success;
        }
    }

    public class DialogResult {
        public string apiKey;
        public string modelName;
        public float latitude;
        public float longitude;
        public DialogResult(string apiKey, string modelName, float latitude, float longitude) {
            this.apiKey = apiKey;
            this.modelName = modelName;
            this.latitude = latitude;
            this.longitude = longitude;
        }
    }

    public class MyDialog : Dialog<DialogResult> {
        private TextBox apiKeyTextBox;
        private DropDown modelDropDown;
        private TextBox latitudeTextBox;
        private TextBox longitudeTextBox;
        private Button authButton;

        public MyDialog() {
            Title = "GDP Plugin Window";
            ClientSize = new Size(400, 400);
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

            var modelLabel = new Label {
                Text = "Model:",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font("Helvetica", 10)
            };

            this.modelDropDown = new DropDown {
                Items = {"Google Maps", "Apple Maps", "Something else"},
                SelectedIndex = 0
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
                    AuthSession.CesiumAccessToken = null;
                    MessageBox.Show("Logging out...");
                    authButton.Text = "Log In";
                    loggedInLabel.Text = "You are not logged in.";
                    return;
                }

                var authCommand = new AuthenticateCommand();
                string? key = authCommand.AuthenticatePublic();

                if (key != null)
                {
                    AuthSession.CesiumAccessToken = key;
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
                Items = { null, modelLabel, this.modelDropDown }
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
                    new StackLayoutItem(null, expand: true),
                    buttons
                }
            };
        }

        private DialogResult getUserInput() {
            string apiKey = AuthSession.CesiumAccessToken;
            string modelName = this.modelDropDown.SelectedValue.ToString();
            string latitudeText = this.latitudeTextBox.Text;
            string longitudeText = this.longitudeTextBox.Text;
            bool canConvertLatitude = float.TryParse(latitudeText, out float latitude);
            bool canConvertLongitude = float.TryParse(longitudeText, out float longitude);
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

            RhinoApp.WriteLine("Fetch Successful! \n" +
                "Model Name: " + modelName + "\n" +
                "Latitude: " + latitude.ToString() + "\n" +
                "Longitude: " + longitude.ToString()
            );

            return new DialogResult(
                apiKey,
                modelName,
                latitude,
                longitude
            );
        }
    }
}
