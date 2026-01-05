using System.ComponentModel;
using ModelContextProtocol.Server;
using SkiaSharp;
using Svg.Skia;

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
    /// Captures the current terminal screen as SVG and PNG.
    /// </summary>
    [McpServerTool, Description("Capture the current terminal screen as an SVG image and save to a file. Also generates a PNG version.")]
    public CaptureScreenshotResult CaptureTerminalScreenshot(
        [Description("The session ID returned by start_terminal")] string sessionId,
        [Description("File path to save the SVG (required). A PNG with the same name will also be generated.")] string savePath)
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

        if (string.IsNullOrWhiteSpace(savePath))
        {
            return new CaptureScreenshotResult
            {
                Success = false,
                SessionId = sessionId,
                Message = "savePath is required. Please provide a file path to save the screenshot."
            };
        }

        try
        {
            var svg = session.CaptureSvg();

            // Ensure directory exists
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Ensure .svg extension
            if (!savePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                savePath += ".svg";
            }

            // Save SVG
            File.WriteAllText(savePath, svg);

            // Generate PNG path
            var pngPath = Path.ChangeExtension(savePath, ".png");

            // Convert SVG to PNG using Svg.Skia
            try
            {
                using var svgDoc = new SKSvg();
                svgDoc.FromSvg(svg);

                if (svgDoc.Picture != null)
                {
                    var bounds = svgDoc.Picture.CullRect;
                    var width = (int)Math.Ceiling(bounds.Width);
                    var height = (int)Math.Ceiling(bounds.Height);

                    if (width > 0 && height > 0)
                    {
                        using var bitmap = new SKBitmap(width, height);
                        using var canvas = new SKCanvas(bitmap);
                        canvas.Clear(SKColors.Black);
                        canvas.DrawPicture(svgDoc.Picture);

                        using var image = SKImage.FromBitmap(bitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = File.OpenWrite(pngPath);
                        data.SaveTo(stream);

                        return new CaptureScreenshotResult
                        {
                            Success = true,
                            SessionId = sessionId,
                            Message = $"Screenshot saved to {savePath} and {pngPath}",
                            SavedPath = savePath,
                            PngPath = pngPath,
                            Width = session.Width,
                            Height = session.Height,
                            HasExited = session.HasExited,
                            ExitCode = session.HasExited ? session.ExitCode : null
                        };
                    }
                }

                // If SVG parsing failed, still return success for the SVG
                return new CaptureScreenshotResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Screenshot saved to {savePath} (PNG conversion failed: empty SVG)",
                    SavedPath = savePath,
                    Width = session.Width,
                    Height = session.Height,
                    HasExited = session.HasExited,
                    ExitCode = session.HasExited ? session.ExitCode : null
                };
            }
            catch (Exception pngEx)
            {
                // SVG was saved, but PNG conversion failed
                return new CaptureScreenshotResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Screenshot saved to {savePath} (PNG conversion failed: {pngEx.Message})",
                    SavedPath = savePath,
                    Width = session.Width,
                    Height = session.Height,
                    HasExited = session.HasExited,
                    ExitCode = session.HasExited ? session.ExitCode : null
                };
            }
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
