using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace LoadTiles;

public class TextInputDialog : Dialog<string?> {
    /* This class is just used to simply get text input from the user.
     * As far as I could tell, the C# language didn't natively have a way to do this. */

    private TextBox textBox;
    public TextInputDialog(string title, string subtitle, string defaultValue) {
        Title = title;
        ClientSize = new Size(300, 240);
        Resizable = false;

        Content = createDialogContent(title, subtitle, defaultValue);
    }

    private DynamicLayout createDialogContent(string title, string subtitle, string defaultValue) {
        var headerPanel = Styling.createHeaderPanel(title, subtitle, false);
        var textInputPanel = createTextInputPanel(defaultValue);
        var buttonsDynamicLayout = createButtonsPanel();

        var components = new List<Panel> {
            headerPanel,
            textInputPanel,
            buttonsDynamicLayout
        };
        return Styling.createDialogContent(components);
    }

    private DynamicLayout createTextInputPanel(string defaultValue) {
        this.textBox = new TextBox {
            Text = defaultValue
        };

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginHorizontal();
        dynamicLayoutInner.Add(this.textBox, true);
        dynamicLayoutInner.EndHorizontal();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();
        
        return dynamicLayout;
    }

    private DynamicLayout createButtonsPanel() {
        DefaultButton = new Button{Text = "OK"};
        DefaultButton.Click += (sender, e) => {
            Close(this.textBox.Text);
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
}