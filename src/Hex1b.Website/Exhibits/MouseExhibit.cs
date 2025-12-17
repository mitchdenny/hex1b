using Hex1b;
using Hex1b.Widgets;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// Demonstrates mouse tracking and click-to-focus support in Hex1b.
/// </summary>
public class MouseExhibit : IGalleryExhibit
{
    public string Id => "mouse";
    public string Title => "Mouse Tracking";
    public string Description => "Demonstrates mouse cursor and click-to-focus";

    private readonly TextBoxState _textBox1 = new() { Text = "Click me!" };
    private readonly TextBoxState _textBox2 = new() { Text = "Or me!" };
    private readonly TextBoxState _textBox3 = new() { Text = "Type here..." };
    
    private int _clickCount = 0;

    public Func<Hex1bWidget> CreateWidgetBuilder()
    {
        return () =>
            new BorderWidget(
                new VStackWidget([
                    new TextBlockWidget("🖱️  Mouse Click-to-Focus Demo"),
                    new TextBlockWidget(""),
                    new TextBlockWidget("Click on any input to focus it:"),
                    new TextBlockWidget(""),
                    new HStackWidget([
                        new TextBlockWidget("Name:    "),
                        new TextBoxWidget(_textBox1) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20) }
                    ]),
                    new HStackWidget([
                        new TextBlockWidget("Email:   "),
                        new TextBoxWidget(_textBox2) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20) }
                    ]),
                    new HStackWidget([
                        new TextBlockWidget("Message: "),
                        new TextBoxWidget(_textBox3) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20) }
                    ]),
                    new TextBlockWidget(""),
                    new TextBlockWidget("Click buttons to activate them:"),
                    new TextBlockWidget(""),
                    new HStackWidget([
                        new ButtonWidget("Count++", () => _clickCount++),
                        new TextBlockWidget($"  Clicks: {_clickCount}")
                    ]),
                    new TextBlockWidget(""),
                    new ButtonWidget("Reset", () => {
                        _clickCount = 0;
                        _textBox1.Text = "Click me!";
                        _textBox2.Text = "Or me!";
                        _textBox3.Text = "Type here...";
                    }),
                    new TextBlockWidget(""),
                    new TextBlockWidget("The yellow cursor shows mouse position.")
                ]),
                "Mouse Test"
            );
    }
}
