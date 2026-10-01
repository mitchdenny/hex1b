using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

// Helper to create WebSocket terminal endpoint
async Task HandleTerminal(HttpContext context, params string[] command)
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
        .WithPtyProcess(command[0], command[1..])
        .Build();

    await terminal.RunAsync(context.RequestAborted);
}

// Star Wars ASCII movie via SSH
app.Map("/ws/starwars", ctx => HandleTerminal(ctx, "ssh", "starwarstel.net"));

// CMatrix - Matrix-style falling code
app.Map("/ws/cmatrix", ctx => HandleTerminal(ctx, 
    "docker", "run", "-it", "--rm", "--log-driver", "none", 
    "--net", "none", "--read-only", "--cap-drop=ALL", "willh/cmatrix"));

// Pipes - animated pipes screensaver
app.Map("/ws/pipes", ctx => HandleTerminal(ctx, "docker", "run", "--rm", "-it", "joonas/pipes.sh"));

// Asciiquarium - underwater ASCII art
app.Map("/ws/asciiquarium", ctx => HandleTerminal(ctx, "docker", "run", "-it", "--rm", "vanessa/asciiquarium"));

app.Run();
