using Eto.Forms;
using Eto.Drawing;
using System.Diagnostics;
using System.Collections.Generic;

namespace LoadTiles;

public class GDPHelpDialog : Dialog<bool> {
    public GDPHelpDialog() {
        Title = "Help";
        ClientSize = new Size(500, 600);

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Help",
            "How to use the LoadTiles plugin",
            false
        );
        var authTextDynamicLayout = createAuthenticationTextPanel();
        var specificityTextDynamicLayout = createSpecificityTextPanel();
        var maskingTextDynamicLayout = createMaskingTextPanel();
        var buttonDynamicLayout = createButtonPanel();

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        dynamicLayout.Add(headerPanel, true);
        dynamicLayout.Add(authTextDynamicLayout, true);
        dynamicLayout.Add(specificityTextDynamicLayout, true);
        dynamicLayout.Add(maskingTextDynamicLayout, true);
        dynamicLayout.Add(buttonDynamicLayout, true);
        dynamicLayout.Add(null, true);
        dynamicLayout.EndVertical();

        return dynamicLayout;
    }

    private DynamicLayout createAuthenticationTextPanel() {
        var authLabel = Styling.label("Authentication", 12);
        var authLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = authLabel
        };

        var longTextLabel = Styling.label(
            "In order to import data, you need to have a Cesium ion access token. You'll first need to sign up for an account. Once you've done that, you should be able to log in to this plugin and start importing data.",
            9
        );
        var linkButton = new LinkButton {
            Text = "Click here to configure Cesium ion."
        };
        linkButton.Click += (sender, e) => {
            Process.Start(new ProcessStartInfo {
                FileName = "https://ion.cesium.com/tokens",
                UseShellExecute = true
            });
        };

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(authLabelPanel);
        dynamicLayoutInner.Add(longTextLabel);
        dynamicLayoutInner.Add(linkButton);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private DynamicLayout createSpecificityTextPanel() {
        var specificityLabel = Styling.label("Specifying what to import", 12);
        var specificityLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = specificityLabel
        };

        var longTextLabel = Styling.label(
            "You can customise what data you'd like to import into the project. You can select which model to import from, e.g. from Google Maps. You can also enter the real world position from which to import: this corresponds to the origin within Rhino.",
            9
        );

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(specificityLabelPanel);
        dynamicLayoutInner.Add(longTextLabel);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private DynamicLayout createMaskingTextPanel() {
        var maskingLabel = Styling.label("Masking", 12);
        var maskingLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = maskingLabel
        };

        var longTextLabel = Styling.label(
            "This plugin has the ability to mask out some of the data imported. You can do this by adding an object to your project to act as the bounds for the masking, and then running the \"Mask\" command to manage the masking objects in your current project. This is saved when you save your project, so that it's simple to perform the same masking again the next time you open the file. You can rename these masking objects to make them easier to manage, and remove them if they're no longer needed.",
            9
        );

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(maskingLabelPanel);
        dynamicLayoutInner.Add(longTextLabel);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
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

        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 30, 0, 10)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(null, true);
        dynamicLayout.Add(buttonPanel);
        dynamicLayout.Add(null, true);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }
}

public class MaskingHelpDialog : Dialog<bool> {
    public MaskingHelpDialog() {
        Title = "Help";
        ClientSize = new Size(500, 450);

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Help",
            "How to use the masking feature",
            false
        );
        var addTextDynamicLayout = createAddTextPanel();
        var modifyTextDynamicLayout = createModifyTextPanel();
        var buttonDynamicLayout = createButtonPanel();

        var components = new List<Panel>() {
            headerPanel,
            addTextDynamicLayout,
            modifyTextDynamicLayout,
            buttonDynamicLayout
        };
        return Styling.createDialogContent(components);
    }

    private DynamicLayout createAddTextPanel() {
        var titleLabel = Styling.label("Adding objects", 12);
        var titleLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = titleLabel
        };

        var longTextLabel = Styling.label(
            "To perform masking, you first need to add a masking object. Click the \"Add\" button at the bottom of the window, which will take you back to Rhino, at which point you can select an object. All the imported model data within the bounds of this object will be masked away. It might be helpful to put this masking object in a separate layer which is invisible, so that it doesn't interfere with the rest of your project.",
            9
        );
        
        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(titleLabelPanel, true);
        dynamicLayoutInner.Add(longTextLabel, true);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private DynamicLayout createModifyTextPanel() {
        var titleLabel = Styling.label("Modifying objects", 12);
        var titleLabelPanel = new Panel {
            Padding = new Padding(0, 0, 0, 10),
            Content = titleLabel
        };

        var longTextLabel = Styling.label(
            "You can rename your masking objects, in order to keep track of which is which. You can also remove any masking objects that you no longer need. If you lose track of which object in the list refers to which object in the project, you can choose to highlight the object, which will show up in Rhino as a yellow outline.",
            9
        );
        
        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(titleLabelPanel, true);
        dynamicLayoutInner.Add(longTextLabel, true);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
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

        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 30, 0, 10)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(null, true);
        dynamicLayout.Add(buttonPanel);
        dynamicLayout.Add(null, true);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }
}