using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws/terminal", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await using var presentation = new WebSocketPresentationAdapter(webSocket, 80, 24);
    
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess("bash", "-i")
        .Build();

    await terminal.RunAsync(context.RequestAborted);
});

app.Run();
