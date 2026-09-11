using System.Net.WebSockets;

namespace WebTerminalDemo;

internal sealed record BrowserCloseRequest(WebSocketCloseStatus Status, string Reason, bool Abort = false)
{
    internal static BrowserCloseRequest? ForFailure(string? mode) => mode switch
    {
        "close" => new(WebSocketCloseStatus.NormalClosure, "Demo: graceful view closure"),
        "abort" => new(WebSocketCloseStatus.Empty, "", Abort: true),
        "policy" => new(WebSocketCloseStatus.PolicyViolation, "Demo: policy violation"),
        "server-error" => new(WebSocketCloseStatus.InternalServerError, "Demo: server failure"),
        _ => null
    };

    internal static BrowserCloseRequest OwnerStopped { get; } = new((WebSocketCloseStatus)4000, "Terminal stopped by owner");
    internal static BrowserCloseRequest ApplicationStopping { get; } = new(WebSocketCloseStatus.EndpointUnavailable, "Application shutting down");
    internal static BrowserCloseRequest WorkloadFailed { get; } = new(WebSocketCloseStatus.InternalServerError, "Terminal workload failed; see server log");
}
