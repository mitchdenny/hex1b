using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class ProgressBarExhibit : IGalleryExhibit
{
    public string Id => "progress-bar";
    public string Title => "Progress Bar";
    public string Description => "Animated progress bar demonstration.";

    public string SourceCode => """
        var progress = new ProgressWidget(0.0);
        var app = new Hex1bApp(() => progress);
        
        _ = Task.Run(async () => {
            for (int i = 0; i <= 100; i++) {
                progress.Value = i / 100.0;
                await Task.Delay(50);
            }
        });
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        var message = "\x1b[1;33mProgress Demo\x1b[0m\r\n\r\n";
        await webSocket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);

        for (int i = 0; i <= 100 && webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested; i += 2)
        {
            var barWidth = 40;
            var filled = (int)(barWidth * i / 100.0);
            var empty = barWidth - filled;

            var bar = $"\x1b[2K\x1b[G  \x1b[36m[\x1b[32m{"█".PadRight(filled, '█')}\x1b[90m{"░".PadRight(empty, '░')}\x1b[36m]\x1b[0m {i,3}%";
            
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(bar), WebSocketMessageType.Text, true, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        await webSocket.SendAsync(Encoding.UTF8.GetBytes("\r\n\r\n\x1b[32m✓ Complete!\x1b[0m\r\n"), WebSocketMessageType.Text, true, cancellationToken);

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
