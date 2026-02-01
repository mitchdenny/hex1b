using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Automation;

namespace Hex1b.McpServer;

/// <summary>
/// A terminal target connected to a remote Hex1b application via Unix domain socket.
/// </summary>
public sealed class RemoteTerminalTarget : ITerminalTarget
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _socketPath;
    private readonly string _id;
    private bool _disposed;

    /// <summary>
    /// Gets the socket path for this remote target.
    /// </summary>
    public string SocketPath => _socketPath;

    /// <inheritdoc />
    public string Id => _id;

    /// <inheritdoc />
    public TerminalTargetType TargetType => TerminalTargetType.Remote;

    /// <inheritdoc />
    public int Width { get; private set; }

    /// <inheritdoc />
    public int Height { get; private set; }

    /// <inheritdoc />
    public int ProcessId { get; private set; }

    /// <inheritdoc />
    public bool IsAlive => !_disposed && IsProcessRunning(ProcessId);

    /// <inheritdoc />
    public DateTimeOffset StartedAt { get; private set; }

    /// <inheritdoc />
    public string Name { get; private set; } = "Unknown";

    private RemoteTerminalTarget(string socketPath, int processId)
    {
        _socketPath = socketPath;
        ProcessId = processId;
        _id = $"remote-{processId}";
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Connects to a remote Hex1b application via its diagnostics socket.
    /// </summary>
    /// <param name="socketPath">Path to the Unix domain socket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected remote terminal target.</returns>
    public static async Task<RemoteTerminalTarget> ConnectAsync(string socketPath, CancellationToken ct = default)
    {
        // Extract PID from socket path (format: [pid].diagnostics.socket)
        var fileName = Path.GetFileName(socketPath);
        var pidStr = fileName.Replace(".diagnostics.socket", "");
        if (!int.TryParse(pidStr, out var pid))
        {
            throw new ArgumentException($"Cannot extract PID from socket path: {socketPath}");
        }

        var target = new RemoteTerminalTarget(socketPath, pid);
        await target.RefreshInfoAsync(ct);
        return target;
    }

    /// <summary>
    /// Connects to a remote Hex1b application by process ID.
    /// </summary>
    /// <param name="processId">The process ID of the Hex1b application.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected remote terminal target.</returns>
    public static Task<RemoteTerminalTarget> ConnectByPidAsync(int processId, CancellationToken ct = default)
    {
        var socketPath = GetSocketPath(processId);
        return ConnectAsync(socketPath, ct);
    }

    /// <summary>
    /// Gets the socket path for a given process ID.
    /// </summary>
    public static string GetSocketPath(int pid)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hex1b", "sockets", $"{pid}.diagnostics.socket");
    }

    /// <summary>
    /// Refreshes the info from the remote target.
    /// </summary>
    public async Task RefreshInfoAsync(CancellationToken ct = default)
    {
        var response = await SendRequestAsync<InfoResponse>(new { method = "info" }, ct);
        if (response.Success)
        {
            Name = response.AppName ?? "Unknown";
            Width = response.Width ?? 0;
            Height = response.Height ?? 0;
            if (response.StartTime.HasValue)
            {
                StartedAt = response.StartTime.Value;
            }
        }
    }

    /// <inheritdoc />
    public async Task SendInputAsync(string text, CancellationToken ct = default)
    {
        await SendRequestAsync<BasicResponse>(new { method = "input", data = text }, ct);
    }

    /// <inheritdoc />
    public async Task SendKeyAsync(string key, string[]? modifiers = null, CancellationToken ct = default)
    {
        // Use the "key" method which injects proper key events via the workload adapter
        await SendRequestAsync<BasicResponse>(new { method = "key", key, modifiers }, ct);
    }

    /// <inheritdoc />
    public async Task SendMouseClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        // Use the "click" method which injects proper mouse events via the workload adapter
        var buttonStr = button switch
        {
            MouseButton.Left => "left",
            MouseButton.Right => "right",
            MouseButton.Middle => "middle",
            _ => "left"
        };
        
        await SendRequestAsync<BasicResponse>(new { method = "click", x, y, button = buttonStr }, ct);
    }

    /// <inheritdoc />
    public async Task<string> CaptureTextAsync(CancellationToken ct = default)
    {
        // Use "text" format for plain text - returns cells in row/column order without ANSI codes
        var response = await SendRequestAsync<CaptureResponse>(new { method = "capture", format = "text" }, ct);
        return response.Data ?? "";
    }

    /// <inheritdoc />
    public async Task<string> CaptureSvgAsync(TerminalSvgOptions? options = null, CancellationToken ct = default)
    {
        var response = await SendRequestAsync<CaptureResponse>(new { method = "capture", format = "svg" }, ct);
        return response.Data ?? "";
    }

    /// <inheritdoc />
    public async Task<string> CaptureAnsiAsync(TerminalAnsiOptions? options = null, CancellationToken ct = default)
    {
        var response = await SendRequestAsync<CaptureResponse>(new { method = "capture", format = "ansi" }, ct);
        return response.Data ?? "";
    }

    /// <inheritdoc />
    public async Task<bool> WaitForTextAsync(string text, TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var screenText = await CaptureTextAsync(cts.Token);
                if (screenText.Contains(text))
                    return true;

                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout occurred
        }

        return false;
    }

    private async Task<T> SendRequestAsync<T>(object request, CancellationToken ct) where T : BasicResponse, new()
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.ReceiveTimeout = 5000;
        socket.SendTimeout = 5000;

        await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        await writer.WriteLineAsync(requestJson);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var responseLine = await reader.ReadLineAsync(cts.Token);
        if (string.IsNullOrEmpty(responseLine))
        {
            return new T { Success = false, Error = "No response from socket" };
        }

        // Handle UTF-8 BOM if present
        if (responseLine.StartsWith('\uFEFF'))
        {
            responseLine = responseLine[1..];
        }

        return JsonSerializer.Deserialize<T>(responseLine, JsonOptions) ?? new T { Success = false, Error = "Invalid response" };
    }

    private static string TranslateKey(string key, string[]? modifiers)
    {
        var hasCtrl = modifiers?.Contains("Ctrl", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasAlt = modifiers?.Contains("Alt", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasShift = modifiers?.Contains("Shift", StringComparer.OrdinalIgnoreCase) ?? false;

        var baseKey = key.ToLowerInvariant() switch
        {
            "enter" or "return" => "\r",
            "tab" => hasShift ? "\x1b[Z" : "\t",
            "escape" or "esc" => "\x1b",
            "backspace" => "\x7f",
            "delete" => "\x1b[3~",
            "up" => "\x1b[A",
            "down" => "\x1b[B",
            "right" => "\x1b[C",
            "left" => "\x1b[D",
            "home" => "\x1b[H",
            "end" => "\x1b[F",
            "pageup" => "\x1b[5~",
            "pagedown" => "\x1b[6~",
            "insert" => "\x1b[2~",
            "f1" => "\x1bOP",
            "f2" => "\x1bOQ",
            "f3" => "\x1bOR",
            "f4" => "\x1bOS",
            "f5" => "\x1b[15~",
            "f6" => "\x1b[17~",
            "f7" => "\x1b[18~",
            "f8" => "\x1b[19~",
            "f9" => "\x1b[20~",
            "f10" => "\x1b[21~",
            "f11" => "\x1b[23~",
            "f12" => "\x1b[24~",
            "space" => " ",
            _ when key.Length == 1 && hasCtrl => ((char)(char.ToUpper(key[0]) - 'A' + 1)).ToString(),
            _ when key.Length == 1 => key,
            _ => ""
        };

        // Alt modifier sends ESC prefix
        if (hasAlt && baseKey.Length > 0)
        {
            return "\x1b" + baseKey;
        }

        return baseKey;
    }

    private static string StripAnsiCodes(string text)
    {
        // Simple regex-free ANSI stripping
        var sb = new StringBuilder();
        var inEscape = false;
        
        foreach (var c in text)
        {
            if (c == '\x1b')
            {
                inEscape = true;
                continue;
            }
            
            if (inEscape)
            {
                // End of CSI sequence
                if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c == '~')
                {
                    inEscape = false;
                }
                continue;
            }
            
            sb.Append(c);
        }
        
        return sb.ToString();
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    // Response types for JSON deserialization
    private class BasicResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    private class InfoResponse : BasicResponse
    {
        [JsonPropertyName("appName")]
        public string? AppName { get; set; }

        [JsonPropertyName("processId")]
        public int? ProcessId { get; set; }

        [JsonPropertyName("startTime")]
        public DateTimeOffset? StartTime { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }
    }

    private class CaptureResponse : BasicResponse
    {
        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }
    }
}
