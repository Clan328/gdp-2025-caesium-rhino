using Rhino;
using Rhino.Commands;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

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

        public MyDialog() {
            Title = "GDP Plugin Window";
            ClientSize = new Size(400, 400);
            Resizable = true; // TODO: maybe remove?

            var titleLabel = new Label {
                Text = "GDP Plugin",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 18, FontStyle.Bold)
            };

            var subtitleLabel = new Label {
                Text = "Enter all of the data in this window",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 10)
            };

            var subtitleLabelPanel = new Panel {
                Padding = new Padding(0, 0, 0, 10),
                Content = subtitleLabel
            };

            var apiKeyLabel = new Label {
                Text = "API key:",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 10)
            };
            this.apiKeyTextBox = new TextBox{Width = 200};

            var modelLabel = new Label {
                Text = "Model:",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 10)
            };

            this.modelDropDown = new DropDown {
                Items = {"Google Maps", "Apple Maps", "Something else"},
                SelectedIndex = 0
            };

            var latitudeLabel = new Label {
                Text = "Latitude:",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 10)
            };
            this.latitudeTextBox = new TextBox();
            var longitudeLabel = new Label {
                Text = "Longitude:",
                VerticalAlignment = VerticalAlignment.Center,
                Font = new Font(SystemFont.Default.ToString(), 10)
            };
            this.longitudeTextBox = new TextBox();

            // Button authButton = new Button{Text = "Click here to do auth"};

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
                    apiKeyTableLayout,
                    modelStackLayout,
                    latitudeStackLayout,
                    longitudeStackLayout,
                    new StackLayoutItem(null, expand: true),
                    buttons
                }
            };
        }

        public DialogResult getUserInput() {
            string apiKey = this.apiKeyTextBox.Text;
            string modelName = this.modelDropDown.SelectedValue.ToString();
            string latitudeText = this.latitudeTextBox.Text;
            string longitudeText = this.longitudeTextBox.Text;
            bool canConvertLatitude = float.TryParse(latitudeText, out float latitude);
            bool canConvertLongitude = float.TryParse(longitudeText, out float longitude);
            bool latitudeValid = canConvertLatitude && latitude >= -90 && latitude <= 90;
            bool longitudeValid = canConvertLongitude && longitude >= -180 && longitude <= 180;
            if (string.IsNullOrWhiteSpace(apiKey)) {
                MessageBox.Show("You have entered an invalid API key.", "Error", MessageBoxType.Error);
                this.apiKeyTextBox.BackgroundColor = Colors.DarkRed;
                this.apiKeyTextBox.TextColor = Colors.White;
                return null;
            } else {
                this.apiKeyTextBox.BackgroundColor = Colors.White;
                this.apiKeyTextBox.TextColor = Colors.Black;
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
            return new DialogResult(
                apiKey,
                modelName,
                latitude,
                longitude
            );
        }
    }
}
