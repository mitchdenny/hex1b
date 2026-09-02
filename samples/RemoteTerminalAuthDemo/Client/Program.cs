using System.Net.WebSockets;
using Hex1b;

var endpoint = args.Length > 0
    ? new Uri(args[0])
    : new Uri("ws://localhost:5050/ws/attach");
var bearerToken = args.Length > 1 ? args[1] : "demo-token";

if (string.IsNullOrWhiteSpace(bearerToken))
{
    Console.Error.WriteLine("The bearer token must not be empty.");
    return 1;
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithRemoteTerminal(
        endpoint,
        options => options.SetRequestHeader(
            "Authorization",
            $"Bearer {bearerToken}"))
    .Build();

try
{
    return await terminal.RunAsync();
}
catch (WebSocketException exception)
{
    Console.Error.WriteLine($"Remote terminal connection failed: {exception.Message}");
    return 1;
}
