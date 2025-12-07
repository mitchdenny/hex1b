using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class ColorPaletteExhibit : IGalleryExhibit
{
    public string Id => "color-palette";
    public string Title => "Color Palette";
    public string Description => "Display of all available terminal colors and styles.";

    public string SourceCode => """
        var colors = new[] { "Red", "Green", "Blue", "Yellow", "Magenta", "Cyan" };
        var stack = new VStackWidget(
            colors.Select(c => new TextBlockWidget(c) { Foreground = c })
        );
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\x1b[1;37m╭─ Standard Colors ─────────────────╮\x1b[0m");
        
        // Standard colors
        string[] colorNames = ["Black", "Red", "Green", "Yellow", "Blue", "Magenta", "Cyan", "White"];
        for (int i = 0; i < 8; i++)
        {
            sb.AppendLine($"\x1b[3{i}m  ██ {colorNames[i],-10}\x1b[0m \x1b[9{i}m  ██ Bright {colorNames[i]}\x1b[0m");
        }
        
        sb.AppendLine("\x1b[1;37m├─ Text Styles ─────────────────────┤\x1b[0m");
        sb.AppendLine("\x1b[1m  Bold Text\x1b[0m");
        sb.AppendLine("\x1b[3m  Italic Text\x1b[0m");
        sb.AppendLine("\x1b[4m  Underlined Text\x1b[0m");
        sb.AppendLine("\x1b[7m  Inverted Text\x1b[0m");
        sb.AppendLine("\x1b[1;37m╰───────────────────────────────────╯\x1b[0m");

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
