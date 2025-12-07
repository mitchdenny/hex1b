using System.Net.WebSockets;
using System.Text;

namespace Gallery.Exhibits;

public class TextInputExhibit : IGalleryExhibit
{
    public string Id => "text-input";
    public string Title => "Text Input";
    public string Description => "Interactive text input with echo.";

    public string SourceCode => """
        var textBox = new TextBoxWidget(
            state: textBoxState,
            placeholder: "Type here..."
        );
        var app = new Hex1bApp(() => textBox);
        """;

    public async Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
    {
        var prompt = "\x1b[1;34mInteractive Echo\x1b[0m\r\n" +
                     "Type anything and press Enter:\r\n\r\n" +
                     "\x1b[36m>\x1b[0m ";

        await webSocket.SendAsync(Encoding.UTF8.GetBytes(prompt), WebSocketMessageType.Text, true, cancellationToken);

        var buffer = new byte[1024];
        var inputBuffer = new StringBuilder();

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var input = Encoding.UTF8.GetString(buffer, 0, result.Count);

                foreach (var c in input)
                {
                    if (c == '\r' || c == '\n')
                    {
                        var line = inputBuffer.ToString();
                        inputBuffer.Clear();

                        var response = $"\r\n\x1b[33mYou typed:\x1b[0m {line}\r\n\x1b[36m>\x1b[0m ";
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(response), WebSocketMessageType.Text, true, cancellationToken);
                    }
                    else if (c == '\x7f' || c == '\b') // Backspace
                    {
                        if (inputBuffer.Length > 0)
                        {
                            inputBuffer.Remove(inputBuffer.Length - 1, 1);
                            await webSocket.SendAsync(Encoding.UTF8.GetBytes("\b \b"), WebSocketMessageType.Text, true, cancellationToken);
                        }
                    }
                    else if (c >= ' ')
                    {
                        inputBuffer.Append(c);
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(c.ToString()), WebSocketMessageType.Text, true, cancellationToken);
                    }
                }
            }
        }
    }
}
