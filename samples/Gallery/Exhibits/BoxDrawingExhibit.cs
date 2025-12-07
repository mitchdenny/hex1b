using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class BoxDrawingExhibit : IGalleryExhibit
{
    public string Id => "box-drawing";
    public string Title => "Box Drawing";
    public string Description => "Unicode box drawing characters for UI borders.";

    public string SourceCode => """
        var panel = new PanelWidget(
            title: "My Panel",
            child: new TextBlockWidget("Panel content here"),
            border: BorderStyle.Rounded
        );
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Sharp corners
        sb.AppendLine("\x1b[1;37m Sharp Corners:\x1b[0m");
        sb.AppendLine("  \x1b[36m┌─────────────────┐\x1b[0m");
        sb.AppendLine("  \x1b[36m│\x1b[0m  Sharp Border   \x1b[36m│\x1b[0m");
        sb.AppendLine("  \x1b[36m└─────────────────┘\x1b[0m");
        sb.AppendLine();

        // Rounded corners
        sb.AppendLine("\x1b[1;37m Rounded Corners:\x1b[0m");
        sb.AppendLine("  \x1b[32m╭─────────────────╮\x1b[0m");
        sb.AppendLine("  \x1b[32m│\x1b[0m  Rounded Border \x1b[32m│\x1b[0m");
        sb.AppendLine("  \x1b[32m╰─────────────────╯\x1b[0m");
        sb.AppendLine();

        // Double line
        sb.AppendLine("\x1b[1;37m Double Line:\x1b[0m");
        sb.AppendLine("  \x1b[33m╔═════════════════╗\x1b[0m");
        sb.AppendLine("  \x1b[33m║\x1b[0m  Double Border  \x1b[33m║\x1b[0m");
        sb.AppendLine("  \x1b[33m╚═════════════════╝\x1b[0m");

        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(sb.ToString()),
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
