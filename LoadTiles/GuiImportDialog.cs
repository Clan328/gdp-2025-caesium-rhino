using System;
using System.Collections.Generic;
using System.Text.Json;
using Eto.Drawing;
using Eto.Forms;

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
        Title = "Cesium ion assets";
        ClientSize = new Size(800, 600);
        Resizable = true;

        Content = createDialogContent(assets);
    }

    private DynamicLayout createDialogContent(List<CesiumAsset> assets) {
        var headerPanel = Styling.createHeaderPanel(
            "Cesium ion assets",
            "These are the assets that you have access to with your Cesium ion account. Select which one you'd like to import.",
            false // TODO: maybe implement help?
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
            TextAlignment = TextAlignment.Left,
            TextColor = Colors.Black,
            Font = new Font(Styling.fontName, 11, descriptionFontStyle)
        };

        var attributionText = asset.attribution == null ? "No asset attribution provided." : asset.attribution;
        var attributionFontStyle = asset.attribution == null ? FontStyle.Italic : FontStyle.None;
        var attributionLabel = new Label {
            Text = attributionText,
            TextAlignment = TextAlignment.Left,
            TextColor = Colors.Black,
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