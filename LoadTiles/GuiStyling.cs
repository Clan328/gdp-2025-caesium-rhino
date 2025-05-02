using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace LoadTiles;

public class Styling {
    public static Color colourVeryLight = Color.FromRgb(0xC9F2C7);
    public static Color colourLighter = Color.FromRgb(0xACECA1);
    public static Color colourLight = Color.FromRgb(0x96BE8C);
    public static Color colourDark = Color.FromRgb(0x629460);
    public static Color colourDarker = Color.FromRgb(0x243119);
    public static string fontName = "Helvetica";

    public static Panel createHeaderPanel(string title, string subtitle, bool includeHelpButton) {
        var headerPanel = new Panel {
            BackgroundColor = colourLight,
            Padding = 20
        };

        var titleLabel = label(title, 18, true);
        var subtitleLabel = label(subtitle, 10);

        var dynamicLayout = new DynamicLayout();
        if (includeHelpButton) {
            var textDynamicLayout = new DynamicLayout();
            textDynamicLayout.BeginVertical();
            textDynamicLayout.Add(titleLabel);
            textDynamicLayout.Add(subtitleLabel);
            textDynamicLayout.EndVertical();

            var helpButton = new Button {Text = "Help"};
            if (title == "Fetch data") {
                helpButton.Click += (sender, e) => {
                    GDPHelpDialog dialog = new GDPHelpDialog();
                    dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
                };
            } else if (title == "Masking") {
                helpButton.Click += (sender, e) => {
                    MaskingHelpDialog dialog = new MaskingHelpDialog();
                    dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
                };
            }

            var buttonDynamicLayout = new DynamicLayout();
            buttonDynamicLayout.BeginVertical();
            buttonDynamicLayout.Add(null, true, true);
            buttonDynamicLayout.Add(helpButton, true, false);
            buttonDynamicLayout.Add(null, true, true);
            buttonDynamicLayout.EndVertical();
            
            dynamicLayout.BeginHorizontal();
            dynamicLayout.Add(textDynamicLayout);
            dynamicLayout.Add(null, true);
            dynamicLayout.Add(buttonDynamicLayout);
            dynamicLayout.EndHorizontal();
        } else {
            dynamicLayout.BeginVertical();
            dynamicLayout.Add(titleLabel);
            dynamicLayout.Add(subtitleLabel);
            dynamicLayout.EndVertical();
        }

        headerPanel.Content = dynamicLayout;

        return headerPanel;
    }

    public static DynamicLayout createDialogContent(List<Panel> components) {
        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        foreach (Panel component in components) {
            dynamicLayout.Add(component, true);
        }
        dynamicLayout.Add(null, true);
        dynamicLayout.EndVertical();
        return dynamicLayout;
    }

    public static Label label(string text, int fontSize, bool bold = false) {
        return new Label{
            Text = text,
            Font = new Font(Styling.fontName, fontSize, bold ? FontStyle.Bold : FontStyle.None)
        };
    }
}