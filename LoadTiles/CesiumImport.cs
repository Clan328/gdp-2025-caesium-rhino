using Rhino;
using Rhino.Commands;
using CesiumAuthentication;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Eto.Forms;
using System.Collections.Generic;
using System.Text.Json;
using Eto.Drawing;
using System;

namespace CesiumImport
{
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

    public class CesiumImportDialog : Dialog<int?> {
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
                    Close(asset.id);
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

    public class CesiumImportCommand : Rhino.Commands.Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "CesiumImport";

        private static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            string? key = AuthSession.Login();
            if (!AuthSession.IsLoggedIn) return Result.Failure;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.CesiumAccessToken);
            string json = Task.Run(() => client.GetStringAsync("https://api.cesium.com/v1/assets?type=3DTILES").Result).GetAwaiter().GetResult();
            List<CesiumAsset> assets = CesiumAssets.FromJson(json);

            CesiumImportDialog dialog = new CesiumImportDialog(assets);
            int? id = dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

            if (id != null) {
                // TODO: Load asset with specified id
                RhinoApp.WriteLine($"Should load asset with id: {id}");
            }

            return Result.Success;
        }
    }
}
