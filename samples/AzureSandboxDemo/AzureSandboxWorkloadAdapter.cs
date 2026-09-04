using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Hex1b;

namespace AzureSandboxDemo;

internal sealed class AzureSandboxWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    internal const string DefaultApiVersion = "2026-02-01-preview";
    internal const string DefaultTokenScope = "https://dynamicsessions.io/.default";

    private readonly ClientWebSocket _webSocket = new();
    private readonly Channel<ReadOnlyMemory<byte>> _output =
        Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _disconnectLock = new();
    private readonly Uri _uri;
    private readonly string _accessToken;
    private readonly string _command;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private Action? _disconnected;
    private bool _disconnectSignaled;
    private bool _disposed;

    internal AzureSandboxWorkloadAdapter(
        AzureSandboxSettings settings,
        string accessToken)
    {
        _uri = BuildUri(settings);
        _accessToken = string.IsNullOrWhiteSpace(accessToken)
            ? throw new ArgumentException("An access token is required.", nameof(accessToken))
            : accessToken;
        _command = settings.Command;
    }

    internal string? SessionId { get; private set; }

    internal int? ExitCode { get; private set; }

    internal string? ErrorMessage { get; private set; }

    public event Action? Disconnected
    {
        add
        {
            var invokeImmediately = false;
            lock (_disconnectLock)
            {
                if (_disconnectSignaled)
                {
                    invokeImmediately = true;
                }
                else
                {
                    _disconnected += value;
                }
            }

            if (invokeImmediately)
            {
                value?.Invoke();
            }
        }
        remove
        {
            lock (_disconnectLock)
            {
                _disconnected -= value;
            }
        }
    }

    internal async Task ConnectAsync(
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
        await _webSocket.ConnectAsync(_uri, cancellationToken);

        await SendAsync(
            new SandboxClientMessage
            {
                Type = "start",
                Start = new SandboxStartMessage
                {
                    Command = _command,
                    Args = [],
                    Environment = new Dictionary<string, string>(),
                    WorkingDirectory = "",
                    Tty = true,
                    Stdin = true,
                    Height = height,
                    Width = width
                }
            },
            cancellationToken);

        var handshake = await ReceiveMessageAsync(cancellationToken)
            ?? throw new InvalidOperationException("ACA closed the WebSocket before the shell handshake.");
        if (handshake.Type != "session_id" || string.IsNullOrWhiteSpace(handshake.Data))
        {
            throw new InvalidOperationException(
                $"Expected an ACA session_id message, received '{handshake.Type ?? "<missing>"}'.");
        }

        SessionId = handshake.Data;
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = ReceivePumpAsync(_receiveCts.Token);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(
        CancellationToken cancellationToken = default)
    {
        while (await _output.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_output.Reader.TryRead(out var data))
            {
                return data;
            }
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask WriteInputAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
        => SendAsync(
            new SandboxClientMessage
            {
                Type = "stdin",
                Data = Convert.ToBase64String(data.Span)
            },
            cancellationToken);

    public ValueTask ResizeAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        return SendAsync(
            new SandboxClientMessage
            {
                Type = "resize",
                Resize = new SandboxResizeMessage
                {
                    Height = height,
                    Width = width
                }
            },
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
        }

        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    null,
                    CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _receiveCts?.Dispose();
        _sendLock.Dispose();
        _webSocket.Dispose();
        Complete();
    }

    private async Task ReceivePumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(cancellationToken);
                if (message is null)
                {
                    break;
                }

                switch (message.Type)
                {
                    case "stdout":
                    case "stderr":
                        if (!string.IsNullOrEmpty(message.Data))
                        {
                            await _output.Writer.WriteAsync(
                                Convert.FromBase64String(message.Data),
                                cancellationToken);
                        }
                        break;

                    case "exit_code":
                        ExitCode = message.ExitCode;
                        return;

                    case "error":
                        ErrorMessage = message.Error ?? "Unknown ACA exec-stream error.";
                        await WriteDiagnosticAsync(ErrorMessage, cancellationToken);
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Invalid ACA protocol message: {ex.Message}";
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid ACA Base64 payload: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            Complete();
        }
    }

    private async ValueTask SendAsync(
        SandboxClientMessage message,
        CancellationToken cancellationToken)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            SandboxProtocolJsonContext.Default.SandboxClientMessage);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _webSocket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<SandboxServerMessage?> ReceiveMessageAsync(
        CancellationToken cancellationToken)
    {
        var rented = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var buffer = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(
                    rented.AsMemory(),
                    cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidOperationException(
                        $"ACA sent an unexpected {result.MessageType} WebSocket frame.");
                }

                buffer.Write(rented, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return JsonSerializer.Deserialize(
                buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)),
                SandboxProtocolJsonContext.Default.SandboxServerMessage)
                ?? throw new JsonException("ACA protocol message was null.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async Task WriteDiagnosticAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes($"\r\nACA sandbox error: {message}\r\n");
        await _output.Writer.WriteAsync(bytes, cancellationToken);
    }

    private void Complete()
    {
        _output.Writer.TryComplete();
        Action? disconnected;
        lock (_disconnectLock)
        {
            if (_disconnectSignaled)
            {
                return;
            }

            _disconnectSignaled = true;
            disconnected = _disconnected;
        }

        disconnected?.Invoke();
    }

    private static Uri BuildUri(AzureSandboxSettings settings)
    {
        var host = $"management.{settings.Region}.azuredevcompute.io";
        var path =
            $"/subscriptions/{Escape(settings.SubscriptionId)}" +
            $"/resourceGroups/{Escape(settings.ResourceGroup)}" +
            $"/sandboxGroups/{Escape(settings.SandboxGroup)}" +
            $"/sandboxes/{Escape(settings.SandboxId)}" +
            "/exec/stream";
        return new UriBuilder(Uri.UriSchemeWss, host)
        {
            Path = path,
            Query = $"api-version={DefaultApiVersion}"
        }.Uri;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}

internal sealed class SandboxClientMessage
{
    public required string Type { get; init; }
    public SandboxStartMessage? Start { get; init; }
    public string? Data { get; init; }
    public SandboxResizeMessage? Resize { get; init; }
}

internal sealed class SandboxStartMessage
{
    public required string Command { get; init; }
    public required string[] Args { get; init; }
    public required Dictionary<string, string> Environment { get; init; }
    public required string WorkingDirectory { get; init; }
    public bool Tty { get; init; }
    public bool Stdin { get; init; }
    public int Height { get; init; }
    public int Width { get; init; }
}

internal sealed class SandboxResizeMessage
{
    public int Height { get; init; }
    public int Width { get; init; }
}

internal sealed class SandboxServerMessage
{
    public string? Type { get; init; }
    public string? Data { get; init; }
    public int? ExitCode { get; init; }
    public string? Error { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SandboxClientMessage))]
[JsonSerializable(typeof(SandboxServerMessage))]
internal sealed partial class SandboxProtocolJsonContext : JsonSerializerContext;
