using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class HelloWorldExhibit : IGalleryExhibit
{
    public string Id => "hello-world";
    public string Title => "Hello World";
    public string Description => "A simple hello world terminal output with ANSI colors.";

    public string SourceCode => """
        var app = new Hex1bApp(() => new TextBlockWidget("Hello, World!")
        {
            Foreground = CustardColor.Green
        });
        await app.RunAsync();
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        var message = "\x1b[32m╔════════════════════════════════════╗\x1b[0m\r\n" +
                      "\x1b[32m║\x1b[0m    \x1b[1;36mHello, World!\x1b[0m                 \x1b[32m║\x1b[0m\r\n" +
                      "\x1b[32m║\x1b[0m    Welcome to Hex1b Gallery        \x1b[32m║\x1b[0m\r\n" +
                      "\x1b[32m╚════════════════════════════════════╝\x1b[0m\r\n" +
                      $"\r\nTerminal size: {session.Cols}x{session.Rows}\r\n";

        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        await KeepAliveAsync(webSocket, cancellationToken);
    }

    private static async Task KeepAliveAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                break;
            }
        }
    }
}
