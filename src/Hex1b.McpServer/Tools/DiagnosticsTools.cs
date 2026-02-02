using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Hex1b.McpServer.Tools;

/// <summary>
/// MCP tools for discovering and capturing Hex1b terminals that have diagnostics enabled.
/// </summary>
[McpServerToolType]
public class DiagnosticsTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Lists all Hex1b terminals that have diagnostics enabled via WithMcpDiagnostics().
    /// Connects to each socket to get info about the running application.
    /// </summary>
    [McpServerTool, Description("Lists all Hex1b terminals that have diagnostics enabled via WithMcpDiagnostics(). Returns information about each running application including name, process ID, and dimensions.")]
    public async Task<GetHex1bStacksResult> GetHex1bStacksWithDiagnosticsEnabled(
        CancellationToken ct = default)
    {
        var socketDir = GetSocketDirectory();
        var stacks = new List<Hex1bStackInfo>();

        if (!Directory.Exists(socketDir))
        {
            return new GetHex1bStacksResult
            {
                Success = true,
                Message = "No Hex1b stacks with diagnostics enabled found.",
                StackCount = 0,
                Stacks = []
            };
        }

        var socketFiles = Directory.GetFiles(socketDir, "*.diagnostics.socket");

        foreach (var socketPath in socketFiles)
        {
            var fileName = Path.GetFileName(socketPath);
            var pidStr = fileName.Replace(".diagnostics.socket", "");
            
            if (!int.TryParse(pidStr, out var pid))
                continue;

            // Check if process is still running
            if (!IsProcessRunning(pid))
            {
                // Clean up stale socket
                try { File.Delete(socketPath); }
                catch { /* ignore */ }
                continue;
            }

            // Try to connect and get info
            var info = await TryGetInfoAsync(socketPath, pid, ct);
            if (info != null)
            {
                stacks.Add(info);
            }
        }

        return new GetHex1bStacksResult
        {
            Success = true,
            Message = stacks.Count > 0 
                ? $"Found {stacks.Count} Hex1b stack(s) with diagnostics enabled."
                : "No Hex1b stacks with diagnostics enabled found.",
            StackCount = stacks.Count,
            Stacks = stacks.ToArray()
        };
    }

    /// <summary>
    /// Captures the terminal state from a Hex1b application with diagnostics enabled.
    /// </summary>
    [McpServerTool, Description("Captures the terminal state from a Hex1b application with diagnostics enabled. Saves the capture to a file as ANSI or SVG format.")]
    public async Task<CaptureHex1bTerminalResult> CaptureHex1bTerminal(
        [Description("Process ID of the Hex1b application to capture")] int processId,
        [Description("File path to save the capture (required).")] string savePath,
        [Description("Capture format: 'ansi' or 'svg' (default: 'ansi')")] string format = "ansi",
        CancellationToken ct = default)
    {
        var socketPath = GetSocketPath(processId);

        if (!File.Exists(socketPath))
        {
            return new CaptureHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"No diagnostics socket found for process {processId}. Ensure the application is running with WithMcpDiagnostics() enabled."
            };
        }

        if (!IsProcessRunning(processId))
        {
            // Clean up stale socket
            try { File.Delete(socketPath); }
            catch { /* ignore */ }

            return new CaptureHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Process {processId} is no longer running."
            };
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

            await using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Send capture request
            var request = new { method = "capture", format = format.ToLowerInvariant() };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));

            // Read response
            var responseLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(responseLine))
            {
                return new CaptureHex1bTerminalResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = "No response from diagnostics socket."
                };
            }

            var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, JsonOptions);
            if (response == null || !response.Success)
            {
                return new CaptureHex1bTerminalResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = response?.Error ?? "Unknown error from diagnostics socket."
                };
            }

            // Save to file
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(savePath, response.Data, ct);

            return new CaptureHex1bTerminalResult
            {
                Success = true,
                ProcessId = processId,
                Message = $"Captured {response.Width}x{response.Height} terminal to {savePath}",
                SavedPath = savePath,
                Format = format.ToLowerInvariant(),
                Width = response.Width ?? 0,
                Height = response.Height ?? 0
            };
        }
        catch (Exception ex)
        {
            return new CaptureHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Failed to capture terminal: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Sends input to a Hex1b application with diagnostics enabled.
    /// </summary>
    [McpServerTool, Description("Sends input characters to a Hex1b application with diagnostics enabled. Use this to interact with the terminal.")]
    public async Task<SendInputToHex1bTerminalResult> SendInputToHex1bTerminal(
        [Description("Process ID of the Hex1b application")] int processId,
        [Description("The input to send. Supports escape sequences like \\n for newline, \\t for tab, \\x1b for escape.")] string input,
        CancellationToken ct = default)
    {
        var socketPath = GetSocketPath(processId);

        if (!File.Exists(socketPath))
        {
            return new SendInputToHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"No diagnostics socket found for process {processId}. Ensure the application is running with WithMcpDiagnostics() enabled."
            };
        }

        if (!IsProcessRunning(processId))
        {
            try { File.Delete(socketPath); }
            catch { /* ignore */ }

            return new SendInputToHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Process {processId} is no longer running."
            };
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

            await using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Process escape sequences in input
            var processedInput = ProcessEscapeSequences(input);

            // Send input request
            var request = new { method = "input", data = processedInput };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));

            // Read response
            var responseLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(responseLine))
            {
                return new SendInputToHex1bTerminalResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = "No response from diagnostics socket."
                };
            }

            var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, JsonOptions);
            if (response == null || !response.Success)
            {
                return new SendInputToHex1bTerminalResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = response?.Error ?? "Unknown error from diagnostics socket."
                };
            }

            return new SendInputToHex1bTerminalResult
            {
                Success = true,
                ProcessId = processId,
                Message = $"Sent input to process {processId}",
                CharactersSent = processedInput.Length
            };
        }
        catch (Exception ex)
        {
            return new SendInputToHex1bTerminalResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Failed to send input: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets the widget/node tree from a Hex1b application for debugging.
    /// </summary>
    [McpServerTool, Description("Gets the widget/node tree, popup stack, and focus ring information from a Hex1b application. Use this to debug hit testing, focus, and layout issues.")]
    public async Task<GetHex1bTreeResult> GetHex1bTree(
        [Description("Process ID of the Hex1b application")] int processId,
        CancellationToken ct = default)
    {
        var socketPath = GetSocketPath(processId);

        if (!File.Exists(socketPath))
        {
            return new GetHex1bTreeResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"No diagnostics socket found for process {processId}. Ensure the application is running with WithMcpDiagnostics() enabled."
            };
        }

        if (!IsProcessRunning(processId))
        {
            try { File.Delete(socketPath); }
            catch { /* ignore */ }

            return new GetHex1bTreeResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Process {processId} is no longer running."
            };
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

            await using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Send tree request
            var request = new { method = "tree" };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));

            // Read response
            var responseLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(responseLine))
            {
                return new GetHex1bTreeResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = "No response from diagnostics socket."
                };
            }

            var response = JsonSerializer.Deserialize<TreeDiagnosticsResponse>(responseLine, JsonOptions);
            if (response == null || !response.Success)
            {
                return new GetHex1bTreeResult
                {
                    Success = false,
                    ProcessId = processId,
                    Message = response?.Error ?? "Unknown error from diagnostics socket."
                };
            }

            return new GetHex1bTreeResult
            {
                Success = true,
                ProcessId = processId,
                Message = $"Retrieved tree from {response.Width}x{response.Height} terminal",
                Width = response.Width ?? 0,
                Height = response.Height ?? 0,
                Tree = response.Tree,
                Popups = response.Popups,
                FocusInfo = response.FocusInfo
            };
        }
        catch (Exception ex)
        {
            return new GetHex1bTreeResult
            {
                Success = false,
                ProcessId = processId,
                Message = $"Failed to get tree: {ex.Message}"
            };
        }
    }

    private static string ProcessEscapeSequences(string input)
    {
        // Process common escape sequences
        return input
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\x1b", "\x1b")
            .Replace("\\e", "\x1b")
            .Replace("\\\\", "\\");
    }

    private static string GetSocketDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hex1b", "sockets");
    }

    private static string GetSocketPath(int pid)
    {
        return Path.Combine(GetSocketDirectory(), $"{pid}.diagnostics.socket");
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

    private static async Task<Hex1bStackInfo?> TryGetInfoAsync(string socketPath, int pid, CancellationToken ct)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ReceiveTimeout = 2000;
            socket.SendTimeout = 2000;
            
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

            await using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Send info request
            var request = new { method = "info" };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));

            // Read response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            
            var responseLine = await reader.ReadLineAsync(cts.Token);
            if (string.IsNullOrEmpty(responseLine))
                return null;

            var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, JsonOptions);
            if (response == null || !response.Success)
                return null;

            return new Hex1bStackInfo
            {
                SocketPath = socketPath,
                AppName = response.AppName ?? "Unknown",
                ProcessId = response.ProcessId ?? pid,
                StartTime = response.StartTime,
                Width = response.Width ?? 0,
                Height = response.Height ?? 0,
                IsResponsive = true
            };
        }
        catch
        {
            // Socket exists but not responsive
            return new Hex1bStackInfo
            {
                SocketPath = socketPath,
                AppName = "Unknown",
                ProcessId = pid,
                Width = 0,
                Height = 0,
                IsResponsive = false
            };
        }
    }

    /// <summary>
    /// Response from diagnostics socket (mirrors protocol types).
    /// </summary>
    private sealed class DiagnosticsResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

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

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
    
    /// <summary>
    /// Response from diagnostics socket for tree method.
    /// </summary>
    private sealed class TreeDiagnosticsResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("tree")]
        public JsonElement? Tree { get; set; }
        
        [JsonPropertyName("popups")]
        public JsonElement? Popups { get; set; }
        
        [JsonPropertyName("focusInfo")]
        public JsonElement? FocusInfo { get; set; }
    }
}

// === Result Types ===

public class GetHex1bStacksResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("stackCount")]
    public required int StackCount { get; init; }

    [JsonPropertyName("stacks")]
    public required Hex1bStackInfo[] Stacks { get; init; }
}

public class Hex1bStackInfo
{
    [JsonPropertyName("socketPath")]
    public required string SocketPath { get; init; }

    [JsonPropertyName("appName")]
    public required string AppName { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("startTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? StartTime { get; init; }

    [JsonPropertyName("width")]
    public required int Width { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }

    [JsonPropertyName("isResponsive")]
    public required bool IsResponsive { get; init; }
}

public class CaptureHex1bTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("savedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SavedPath { get; init; }

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}

public class SendInputToHex1bTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("charactersSent")]
    public int CharactersSent { get; init; }
}

public class GetHex1bTreeResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
    
    [JsonPropertyName("tree")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Tree { get; init; }
    
    [JsonPropertyName("popups")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Popups { get; init; }
    
    [JsonPropertyName("focusInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? FocusInfo { get; init; }
}
