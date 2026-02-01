using System.ComponentModel;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Hex1b.McpServer.Tools;

/// <summary>
/// Unified MCP tools for interacting with terminal targets (both local and remote).
/// </summary>
[McpServerToolType]
public class UnifiedTerminalTools(TerminalSessionManager sessionManager)
{
    /// <summary>
    /// Connects to a remote Hex1b application with diagnostics enabled.
    /// </summary>
    [McpServerTool, Description("Connects to a remote Hex1b application by process ID. The application must have diagnostics enabled via WithMcpDiagnostics(). Returns a session ID for use with other terminal tools.")]
    public async Task<ConnectRemoteResult> ConnectToHex1bStack(
        [Description("Process ID of the Hex1b application to connect to")] int processId,
        CancellationToken ct = default)
    {
        try
        {
            var target = await sessionManager.ConnectRemoteAsync(processId, ct);
            
            return new ConnectRemoteResult
            {
                Success = true,
                SessionId = target.Id,
                Message = $"Connected to {target.Name} (PID: {target.ProcessId}, {target.Width}x{target.Height})",
                AppName = target.Name,
                ProcessId = target.ProcessId,
                Width = target.Width,
                Height = target.Height
            };
        }
        catch (Exception ex)
        {
            return new ConnectRemoteResult
            {
                Success = false,
                SessionId = null,
                Message = $"Failed to connect: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Discovers and connects to all Hex1b applications with diagnostics enabled.
    /// </summary>
    [McpServerTool, Description("Discovers and connects to all Hex1b applications that have diagnostics enabled. Returns session IDs for each connected application.")]
    public async Task<DiscoverAndConnectResult> DiscoverHex1bStacks(
        CancellationToken ct = default)
    {
        try
        {
            var targets = await sessionManager.DiscoverAndConnectRemotesAsync(ct);
            
            return new DiscoverAndConnectResult
            {
                Success = true,
                Message = targets.Count > 0 
                    ? $"Connected to {targets.Count} Hex1b stack(s)."
                    : "No new Hex1b stacks found to connect.",
                ConnectedCount = targets.Count,
                Sessions = targets.Select(t => new ConnectedSessionInfo
                {
                    SessionId = t.Id,
                    AppName = t.Name,
                    ProcessId = t.ProcessId,
                    Width = t.Width,
                    Height = t.Height
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            return new DiscoverAndConnectResult
            {
                Success = false,
                Message = $"Discovery failed: {ex.Message}",
                ConnectedCount = 0,
                Sessions = []
            };
        }
    }

    /// <summary>
    /// Lists all connected terminal targets (both local and remote).
    /// </summary>
    [McpServerTool, Description("Lists all terminal targets - both local terminals started by the MCP server and remote Hex1b applications connected via diagnostics.")]
    public ListTargetsResult ListAllTerminalTargets()
    {
        var targets = sessionManager.ListTargets();
        
        return new ListTargetsResult
        {
            Success = true,
            TargetCount = targets.Count,
            Targets = targets.Select(t => new TerminalTargetInfo
            {
                SessionId = t.Id,
                TargetType = t.TargetType.ToString(),
                Name = t.Name,
                ProcessId = t.ProcessId,
                Width = t.Width,
                Height = t.Height,
                IsAlive = t.IsAlive,
                StartedAt = t.StartedAt
            }).ToArray()
        };
    }

    /// <summary>
    /// Sends a mouse click to a terminal target.
    /// </summary>
    [McpServerTool, Description("Sends a mouse click to a terminal target at the specified cell position. Works with both local and remote terminals.")]
    public async Task<SendMouseClickResult> SendTerminalMouseClick(
        [Description("Session ID of the terminal target")] string sessionId,
        [Description("Column position (0-based)")] int x,
        [Description("Row position (0-based)")] int y,
        [Description("Mouse button: 'left', 'middle', or 'right' (default: 'left')")] string button = "left",
        CancellationToken ct = default)
    {
        var target = sessionManager.GetTarget(sessionId);
        if (target == null)
        {
            return new SendMouseClickResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found. Use list_all_terminal_targets to see available sessions."
            };
        }

        var mouseButton = button.ToLowerInvariant() switch
        {
            "middle" => MouseButton.Middle,
            "right" => MouseButton.Right,
            _ => MouseButton.Left
        };

        try
        {
            await target.SendMouseClickAsync(x, y, mouseButton, ct);
            
            return new SendMouseClickResult
            {
                Success = true,
                SessionId = sessionId,
                Message = $"Clicked {button} at ({x}, {y})",
                X = x,
                Y = y,
                Button = button
            };
        }
        catch (Exception ex)
        {
            return new SendMouseClickResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to send mouse click: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Sends a key press to a terminal target.
    /// </summary>
    [McpServerTool, Description("Sends a key press to a terminal target. Supports special keys like Enter, Tab, Escape, arrow keys, F1-F12, etc. Works with both local and remote terminals.")]
    public async Task<SendKeyResult> SendTerminalKey(
        [Description("Session ID of the terminal target")] string sessionId,
        [Description("Key to send: Enter, Tab, Escape, Up, Down, Left, Right, Backspace, Delete, Home, End, PageUp, PageDown, F1-F12, or a single character")] string key,
        [Description("Optional modifiers: Ctrl, Alt, Shift (comma-separated)")] string? modifiers = null,
        CancellationToken ct = default)
    {
        var target = sessionManager.GetTarget(sessionId);
        if (target == null)
        {
            return new SendKeyResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found."
            };
        }

        var modifierArray = string.IsNullOrEmpty(modifiers) 
            ? null 
            : modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            await target.SendKeyAsync(key, modifierArray, ct);
            
            var modStr = modifierArray != null ? $" with {string.Join("+", modifierArray)}" : "";
            return new SendKeyResult
            {
                Success = true,
                SessionId = sessionId,
                Message = $"Sent key '{key}'{modStr}",
                Key = key,
                Modifiers = modifierArray
            };
        }
        catch (Exception ex)
        {
            return new SendKeyResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to send key: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Captures the terminal screen as text, ANSI, or SVG.
    /// </summary>
    [McpServerTool, Description("Captures the terminal screen from any connected target. Can save to a file or return the content. Works with both local and remote terminals.")]
    public async Task<CaptureTerminalResult> CaptureTerminalScreen(
        [Description("Session ID of the terminal target")] string sessionId,
        [Description("Capture format: 'text', 'ansi', or 'svg' (default: 'text')")] string format = "text",
        [Description("Optional file path to save the capture. If not provided, content is returned in the response.")] string? savePath = null,
        CancellationToken ct = default)
    {
        var target = sessionManager.GetTarget(sessionId);
        if (target == null)
        {
            return new CaptureTerminalResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found."
            };
        }

        try
        {
            string content;
            switch (format.ToLowerInvariant())
            {
                case "svg":
                    content = await target.CaptureSvgAsync(null, ct);
                    break;
                case "ansi":
                    content = await target.CaptureAnsiAsync(null, ct);
                    break;
                default:
                    content = await target.CaptureTextAsync(ct);
                    break;
            }

            if (!string.IsNullOrEmpty(savePath))
            {
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(savePath, content, ct);
                
                return new CaptureTerminalResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Captured {target.Width}x{target.Height} terminal to {savePath}",
                    Format = format,
                    SavedPath = savePath,
                    Width = target.Width,
                    Height = target.Height
                };
            }
            else
            {
                return new CaptureTerminalResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Captured {target.Width}x{target.Height} terminal",
                    Format = format,
                    Content = content,
                    Width = target.Width,
                    Height = target.Height
                };
            }
        }
        catch (Exception ex)
        {
            return new CaptureTerminalResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to capture: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Waits for specific text to appear on the terminal screen.
    /// </summary>
    [McpServerTool, Description("Waits for specific text to appear on the terminal screen. Useful for waiting for prompts or specific output. Works with both local and remote terminals.")]
    public async Task<WaitForTextResult> WaitForTerminalText(
        [Description("Session ID of the terminal target")] string sessionId,
        [Description("The text to wait for")] string text,
        [Description("Maximum seconds to wait (default: 10)")] int timeoutSeconds = 10,
        CancellationToken ct = default)
    {
        var target = sessionManager.GetTarget(sessionId);
        if (target == null)
        {
            return new WaitForTextResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found.",
                Found = false
            };
        }

        try
        {
            var found = await target.WaitForTextAsync(text, TimeSpan.FromSeconds(timeoutSeconds), ct);
            
            return new WaitForTextResult
            {
                Success = true,
                SessionId = sessionId,
                Message = found ? $"Found text '{text}'" : $"Text '{text}' not found within {timeoutSeconds}s",
                Found = found
            };
        }
        catch (Exception ex)
        {
            return new WaitForTextResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Wait failed: {ex.Message}",
                Found = false
            };
        }
    }
}

// === Result Types ===

public class ConnectRemoteResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("appName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppName { get; init; }

    [JsonPropertyName("processId")]
    public int? ProcessId { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}

public class DiscoverAndConnectResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("connectedCount")]
    public required int ConnectedCount { get; init; }

    [JsonPropertyName("sessions")]
    public required ConnectedSessionInfo[] Sessions { get; init; }
}

public class ConnectedSessionInfo
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("appName")]
    public required string AppName { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("width")]
    public required int Width { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }
}

public class ListTargetsResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("targetCount")]
    public required int TargetCount { get; init; }

    [JsonPropertyName("targets")]
    public required TerminalTargetInfo[] Targets { get; init; }
}

public class TerminalTargetInfo
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("targetType")]
    public required string TargetType { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("width")]
    public required int Width { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }

    [JsonPropertyName("isAlive")]
    public required bool IsAlive { get; init; }

    [JsonPropertyName("startedAt")]
    public required DateTimeOffset StartedAt { get; init; }
}

public class SendMouseClickResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("x")]
    public int X { get; init; }

    [JsonPropertyName("y")]
    public int Y { get; init; }

    [JsonPropertyName("button")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Button { get; init; }
}

public class SendKeyResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }

    [JsonPropertyName("modifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Modifiers { get; init; }
}

public class CaptureTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    [JsonPropertyName("savedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SavedPath { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}

// WaitForTextResult is defined in ToolResults.cs
