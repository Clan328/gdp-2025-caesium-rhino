using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace LoadTiles;

public class MaskingDialog : Dialog<bool> {
    private MaskingCommand maskingCommand;
    private DynamicLayout objectsDynamicLayout;
    private Dictionary<Guid, Panel> objectPanels;
    public MaskingDialog(MaskingCommand maskingCommand) {
        Title = "Masking options";
        ClientSize = new Size(600, 400);
        Resizable = true;
        
        this.maskingCommand = maskingCommand;

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Masking",
            "How do you want to mask away certain portions of the imported data?",
            false // TODO: implement help for this?
        );
        var descriptionTextDynamicLayout = createDescriptionTextPanel();
        var objectsListDynamicLayout = createObjectsListPanel();
        var buttonPanel = createButtonPanel();

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        dynamicLayout.Add(headerPanel, true);
        dynamicLayout.Add(descriptionTextDynamicLayout, true);
        dynamicLayout.Add(objectsListDynamicLayout, true, true);
        dynamicLayout.Add(buttonPanel, true);
        dynamicLayout.EndVertical();

        return dynamicLayout;
    }

    private DynamicLayout createDescriptionTextPanel() {
        var longTextLabel = Styling.label(
            "Below is a list of the objects from which the masking will be performed. You can add or remove masking objects, and rename them to make them easier to manage.",
            9
        );
        var boldTextLabel = new Label {
            Text = "Any changes made here will be reflected the next time any data is imported.",
            Font = new Font(Styling.fontName, 9, FontStyle.Bold)
        };

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(longTextLabel);
        dynamicLayoutInner.Add(boldTextLabel);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private Panel createObjectsListPanel() {
        this.objectPanels = new Dictionary<Guid, Panel>();

        this.objectsDynamicLayout = new DynamicLayout {
            Padding = 15,
            Height = -1
        };
        objectsDynamicLayout.BeginVertical();

        foreach (Guid maskingObject in this.maskingCommand.maskingObjects) {
            var objectPanel = createObjectPanel(maskingObject);
            objectsDynamicLayout.Add(objectPanel, true, false);
            this.objectPanels[maskingObject] = objectPanel;
        }

        objectsDynamicLayout.EndVertical();

        var objectsScrollable = new Scrollable {
            BackgroundColor = Styling.colourDark,
            Border = BorderType.None,
            ExpandContentHeight = false,
            Content = objectsDynamicLayout
        };
        var objectsPanel = new Panel {
            Padding = new Padding(20, 20, 20, 0),
            Content = objectsScrollable
        };

        return objectsPanel;
    }

    private Panel createObjectPanel(Guid objectId) {
        var colourPicker = new ColorPicker {
            Value = Color.FromRgb(0xFF0000)
        };

        string nameText = objectId.ToString();

        var nameLabel = Styling.label(nameText, 10);
        var guidLabel = new Label {
            Text = "(" + objectId.ToString() + ")",
            Font = new Font(Styling.fontName, 9, FontStyle.Italic)
        };
        var nameDynamicLayout = new DynamicLayout {
            Padding = new Padding(10, 0, 10, 0)
        };
        nameDynamicLayout.BeginVertical();
        nameDynamicLayout.Add(nameLabel);
        nameDynamicLayout.Add(guidLabel);
        nameDynamicLayout.EndVertical();

        var renameButton = new Button {
            Text = "Rename"
        };
        var deleteButton = new Button {
            Text = "Remove"
        };
        deleteButton.Click += (sender, e) => {
            maskingCommand.maskingObjects.Remove(objectId);
            this.objectsDynamicLayout.Remove(this.objectPanels[objectId]); // TODO: fix this
            this.objectsDynamicLayout.Create();
        };

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourLight,
            Padding = 10
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(colourPicker, false);
        dynamicLayout.Add(nameDynamicLayout, true);
        dynamicLayout.Add(renameButton, false);
        dynamicLayout.Add(deleteButton, false);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private DynamicLayout createButtonPanel() {
        AbortButton = new Button{Text = "OK"};
        AbortButton.Click += (sender, e) => Close(true);

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