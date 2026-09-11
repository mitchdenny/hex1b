using System.Net.Http;
using System.Net.WebSockets;

namespace Hex1b;

/// <summary>
/// Configures a remote terminal WebSocket connection and its opening HTTP request.
/// </summary>
public sealed class RemoteTerminalOptions
{
    private readonly Dictionary<string, string?> _requestHeaders =
        new(StringComparer.OrdinalIgnoreCase);
    private Action<ClientWebSocketOptions>? _configureWebSocket;
    private Action<SocketsHttpHandler>? _configureHttpHandler;
    private Action<HttpRequestMessage>? _configureRequest;

    /// <summary>
    /// Adds or replaces a header on the WebSocket opening request.
    /// </summary>
    /// <param name="headerName">The name of the header to configure.</param>
    /// <param name="headerValue">The header value, or <see langword="null"/> to remove it.</param>
    public void SetRequestHeader(string headerName, string? headerValue)
        => _requestHeaders[headerName] = headerValue;

    /// <summary>
    /// Adds a callback that configures the underlying WebSocket before connecting.
    /// </summary>
    /// <param name="configure">
    /// The callback that configures the WebSocket options.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public void ConfigureWebSocket(Action<ClientWebSocketOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureWebSocket += configure;
    }

    /// <summary>
    /// Adds a callback that configures the HTTP handler used to send the opening request.
    /// </summary>
    /// <param name="configure">
    /// The callback that configures the HTTP handler.
    /// </param>
    /// <remarks>
    /// Configure transport settings such as proxies, credentials, cookies, and certificate
    /// validation here rather than on <see cref="ClientWebSocketOptions"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public void ConfigureHttpHandler(Action<SocketsHttpHandler> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureHttpHandler += configure;
    }

    /// <summary>
    /// Adds a callback that can mutate each outgoing WebSocket opening request.
    /// </summary>
    /// <param name="configure">
    /// The callback that configures the outgoing request.
    /// </param>
    /// <remarks>
    /// The callback runs after the WebSocket handshake headers have been added and may be
    /// invoked more than once if the connection retries with another HTTP version.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public void ConfigureRequest(Action<HttpRequestMessage> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureRequest += configure;
    }

    internal bool RequiresHttpMessageInvoker =>
        _configureRequest is not null || _configureHttpHandler is not null;

    internal void ApplyClientWebSocketOptions(ClientWebSocketOptions options)
    {
        _configureWebSocket?.Invoke(options);
        foreach (var (headerName, headerValue) in _requestHeaders)
            options.SetRequestHeader(headerName, headerValue);
    }

    internal void ApplyHttpHandlerOptions(SocketsHttpHandler handler)
        => _configureHttpHandler?.Invoke(handler);

    internal void ApplyRequestOptions(HttpRequestMessage request)
        => _configureRequest?.Invoke(request);
}
