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

    private string _textBox1 = "Click me!";
    private string _textBox2 = "Or me!";
    private string _textBox3 = "Type here...";
    
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
                        new TextBoxWidget(_textBox1) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20), OnTextChanged = args => { _textBox1 = args.NewText; return Task.CompletedTask; } }
                    ]),
                    new HStackWidget([
                        new TextBlockWidget("Email:   "),
                        new TextBoxWidget(_textBox2) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20), OnTextChanged = args => { _textBox2 = args.NewText; return Task.CompletedTask; } }
                    ]),
                    new HStackWidget([
                        new TextBlockWidget("Message: "),
                        new TextBoxWidget(_textBox3) { WidthHint = Hex1b.Layout.SizeHint.Fixed(20), OnTextChanged = args => { _textBox3 = args.NewText; return Task.CompletedTask; } }
                    ]),
                    new TextBlockWidget(""),
                    new TextBlockWidget("Click buttons to activate them:"),
                    new TextBlockWidget(""),
                    new HStackWidget([
                        new ButtonWidget("Count++") { OnClick = _ => { _clickCount++; return Task.CompletedTask; } },
                        new TextBlockWidget($"  Clicks: {_clickCount}")
                    ]),
                    new TextBlockWidget(""),
                    new ButtonWidget("Reset") { OnClick = _ => {
                        _clickCount = 0;
                        _textBox1 = "Click me!";
                        _textBox2 = "Or me!";
                        _textBox3 = "Type here...";
                        return Task.CompletedTask;
                    }},
                    new TextBlockWidget(""),
                    new TextBlockWidget("The yellow cursor shows mouse position.")
                ]),
                "Mouse Test"
            );
    }
}
