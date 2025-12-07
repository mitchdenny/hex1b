using System.Net.WebSockets;
using Hex1b.Widgets;

namespace Gallery.Exhibits;

public class HelloWorldExhibit : Hex1bExhibit
{
    public override string Id => "hello-world";
    public override string Title => "Hello World";
    public override string Description => "A simple hello world using Hex1b widgets.";

    public override string SourceCode => """
        var app = new Hex1bApp(ct => Task.FromResult<Hex1bWidget>(
            new VStackWidget([
                new TextBlockWidget("╔════════════════════════════════════╗"),
                new TextBlockWidget("║    Hello, World!                   ║"),
                new TextBlockWidget("║    Welcome to Hex1b Gallery        ║"),
                new TextBlockWidget("╚════════════════════════════════════╝"),
                new TextBlockWidget(""),
                new TextBlockWidget("Press any key to interact...")
            ])
        ));
        await app.RunAsync();
        """;

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        return ct => Task.FromResult<Hex1bWidget>(
            new VStackWidget([
                new TextBlockWidget("╔════════════════════════════════════╗"),
                new TextBlockWidget("║    Hello, World!                   ║"),
                new TextBlockWidget("║    Welcome to Hex1b Gallery        ║"),
                new TextBlockWidget("╚════════════════════════════════════╝"),
                new TextBlockWidget(""),
                new TextBlockWidget("Press any key to interact...")
            ])
        );
    }
}
