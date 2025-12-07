using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Gallery.Exhibits;

public class TextInputExhibit(ILogger<TextInputExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<TextInputExhibit> _logger = logger;

    public override string Id => "text-input";
    public override string Title => "Text Input";
    public override string Description => "Interactive text input using Hex1b TextBox widget.";

    public override string SourceCode => """
        var textBoxState = new TextBoxState();
        var app = new Hex1bApp(ct => Task.FromResult<Hex1bWidget>(
            new VStackWidget([
                new TextBlockWidget("Interactive Text Input"),
                new TextBlockWidget("─────────────────────────"),
                new TextBlockWidget(""),
                new TextBoxWidget(textBoxState),
                new TextBlockWidget(""),
                new TextBlockWidget("Type something and press Enter!")
            ])
        ));
        await app.RunAsync();
        """;

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        var textBoxState = new TextBoxState();
        
        return ct => Task.FromResult<Hex1bWidget>(
            new VStackWidget([
                new TextBlockWidget("Interactive Text Input"),
                new TextBlockWidget("─────────────────────────"),
                new TextBlockWidget(""),
                new TextBoxWidget(textBoxState),
                new TextBlockWidget(""),
                new TextBlockWidget("Type something! Use Backspace to delete.")
            ])
        );
    }
}
