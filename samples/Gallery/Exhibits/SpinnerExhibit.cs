using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class SpinnerExhibit : IGalleryExhibit
{
    public string Id => "spinner";
    public string Title => "Spinner";
    public string Description => "Animated loading spinner demonstration.";

    public string SourceCode => """
        var spinner = new SpinnerWidget();
        var app = new Hex1bApp(() => new HStackWidget(
            spinner,
            new TextBlockWidget(" Loading...")
        ));
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        string[] spinners = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
        string[] messages = ["Connecting...", "Loading data...", "Processing...", "Almost done...", "Finishing up..."];

        await webSocket.SendAsync(Encoding.UTF8.GetBytes("\x1b[1;35mSpinner Demo\x1b[0m\r\n\r\n"), WebSocketMessageType.Text, true, cancellationToken);

        for (int msg = 0; msg < messages.Length && webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested; msg++)
        {
            for (int i = 0; i < 20 && webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested; i++)
            {
                var spinner = spinners[i % spinners.Length];
                var line = $"\x1b[2K\x1b[G  \x1b[36m{spinner}\x1b[0m {messages[msg]}";
                await webSocket.SendAsync(Encoding.UTF8.GetBytes(line), WebSocketMessageType.Text, true, cancellationToken);
                await Task.Delay(100, cancellationToken);
            }
        }

        await webSocket.SendAsync(Encoding.UTF8.GetBytes("\x1b[2K\x1b[G  \x1b[32m✓\x1b[0m Done!\r\n"), WebSocketMessageType.Text, true, cancellationToken);

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
