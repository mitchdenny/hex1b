using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Hex1b.McpServer.Tools;

/// <summary>
/// MCP tools for dynamic asciinema recording of terminal sessions.
/// </summary>
[McpServerToolType]
public class RecordingTools(TerminalSessionManager sessionManager)
{
    /// <summary>
    /// Starts recording the terminal session to an asciinema file.
    /// </summary>
    [McpServerTool, Description("Start recording a terminal session to an asciinema file. The current terminal state is captured as the initial frame. Use stop_asciinema_recording to finish.")]
    public async Task<StartRecordingResult> StartAsciinemaRecording(
        [Description("The session ID returned by start_bash_terminal or start_pwsh_terminal")] string sessionId,
        [Description("Path to save the asciinema recording file (.cast extension recommended)")] string filePath,
        [Description("Optional title for the recording")] string? title = null,
        CancellationToken ct = default)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return new StartRecordingResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found."
            };
        }

        if (session.IsRecording)
        {
            return new StartRecordingResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' is already recording to '{session.ActiveRecordingPath}'. Stop it first with stop_asciinema_recording."
            };
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Ensure .cast extension
            if (!filePath.EndsWith(".cast", StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.ChangeExtension(filePath, ".cast");
            }

            var options = new AsciinemaRecorderOptions
            {
                AutoFlush = true,
                Title = title ?? $"{session.Command} session"
            };

            await session.StartRecordingAsync(filePath, options, ct);

            return new StartRecordingResult
            {
                Success = true,
                SessionId = sessionId,
                Message = $"Started recording to '{filePath}'. The current terminal state has been captured as the initial frame.",
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            return new StartRecordingResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to start recording: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Stops recording the terminal session.
    /// </summary>
    [McpServerTool, Description("Stop recording a terminal session. The recording file is finalized and can be played back with asciinema.")]
    public async Task<StopRecordingResult> StopAsciinemaRecording(
        [Description("The session ID returned by start_bash_terminal or start_pwsh_terminal")] string sessionId,
        CancellationToken ct = default)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return new StopRecordingResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Session '{sessionId}' not found.",
                WasRecording = false
            };
        }

        var wasRecording = session.IsRecording;
        var activePath = session.ActiveRecordingPath;

        try
        {
            var completedPath = await session.StopRecordingAsync(ct);

            if (completedPath != null)
            {
                return new StopRecordingResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = $"Recording stopped. File saved to '{completedPath}'.",
                    FilePath = completedPath,
                    WasRecording = true
                };
            }
            else
            {
                return new StopRecordingResult
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = "Session was not recording.",
                    WasRecording = false
                };
            }
        }
        catch (Exception ex)
        {
            return new StopRecordingResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Failed to stop recording: {ex.Message}",
                FilePath = activePath,
                WasRecording = wasRecording
            };
        }
    }
}
