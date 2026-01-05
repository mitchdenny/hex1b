using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerminalMcp.Tools;

/// <summary>
/// MCP tools for capturing terminal output.
/// </summary>
[McpServerToolType]
public class CaptureTools(TerminalSessionManager sessionManager)
{
    /// <summary>
    /// Captures the current terminal screen as text.
    /// </summary>
    [McpServerTool, Description("Capture the current terminal screen content as plain text.")]
    public CaptureTextResult CaptureTerminalText(
        [Description("The session ID returned by start_terminal")] string sessionId)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return new CaptureTextResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found."
            };
        }

        try
        {
            var text = session.CaptureText();

            return new CaptureTextResult
            {
                Success = true,
                SessionId = sessionId,
                Message = "Terminal screen captured.",
                Text = text,
                Width = session.Width,
                Height = session.Height,
                HasExited = session.HasExited,
                ExitCode = session.HasExited ? session.ExitCode : null
            };
        }
        catch (Exception ex)
        {
            return new CaptureTextResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to capture text: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Captures the current terminal screen as SVG.
    /// </summary>
    [McpServerTool, Description("Capture the current terminal screen as an SVG image. Optionally save to a file.")]
    public CaptureScreenshotResult CaptureTerminalScreenshot(
        [Description("The session ID returned by start_terminal")] string sessionId,
        [Description("Optional file path to save the SVG. If not provided, SVG content is returned in the response.")] string? savePath = null)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return new CaptureScreenshotResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found."
            };
        }

        try
        {
            var svg = session.CaptureSvg();

            if (!string.IsNullOrEmpty(savePath))
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(savePath, svg);

                return new CaptureScreenshotResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Screenshot saved to {savePath}",
                    SavedPath = savePath,
                    Width = session.Width,
                    Height = session.Height,
                    HasExited = session.HasExited,
                    ExitCode = session.HasExited ? session.ExitCode : null
                };
            }

            return new CaptureScreenshotResult
            {
                Success = true,
                SessionId = sessionId,
                Message = "Terminal screenshot captured.",
                SvgContent = svg,
                Width = session.Width,
                Height = session.Height,
                HasExited = session.HasExited,
                ExitCode = session.HasExited ? session.ExitCode : null
            };
        }
        catch (Exception ex)
        {
            return new CaptureScreenshotResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to capture screenshot: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Waits for specific text to appear on the terminal screen.
    /// </summary>
    [McpServerTool, Description("Wait for specific text to appear on the terminal screen. Useful for waiting for prompts or output.")]
    public async Task<WaitForTextResult> WaitForTerminalText(
        [Description("The session ID returned by start_terminal")] string sessionId,
        [Description("The text to wait for")] string text,
        [Description("Maximum seconds to wait (default: 10)")] int timeoutSeconds = 10,
        CancellationToken ct = default)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session == null)
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
            var timeout = TimeSpan.FromSeconds(Math.Max(1, Math.Min(timeoutSeconds, 60)));
            var found = await session.WaitForTextAsync(text, timeout, ct);

            if (found)
            {
                return new WaitForTextResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Text '{text}' found on terminal.",
                    Found = true
                };
            }
            else
            {
                return new WaitForTextResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Text '{text}' not found within {timeoutSeconds} seconds.",
                    Found = false,
                    CurrentText = session.CaptureText()
                };
            }
        }
        catch (Exception ex)
        {
            return new WaitForTextResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to wait for text: {ex.Message}",
                Found = false
            };
        }
    }
}
