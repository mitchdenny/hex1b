using System.CommandLine;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Hex1b;
using Hex1b.Diagnostics;
using Hex1b.Input;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Attaches to a terminal, streaming its output to the local terminal
/// and forwarding local input to it. Ctrl+] to enter command mode.
/// </summary>
internal sealed class TerminalAttachCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<bool> s_resizeOption = new("--resize") { Description = "Resize remote terminal to match local terminal dimensions" };
    private static readonly Option<bool> s_leadOption = new("--lead") { Description = "Claim resize leadership (only the leader's resize events control the remote terminal)" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalAttachCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalAttachCommand> logger)
        : base("attach", "Attach to a terminal (Ctrl+] for commands)", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_resizeOption);
        Options.Add(s_leadOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Formatter.WriteError("Attach is only supported on Linux and macOS");
            return 1;
        }

        return await ExecuteUnixAsync(parseResult, cancellationToken);
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private async Task<int> ExecuteUnixAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        // Use the Hex1b console driver for proper raw mode and I/O
        using var driver = new UnixConsoleDriver();

        // Resize remote terminal to match local dimensions before attaching
        if (parseResult.GetValue(s_resizeOption))
        {
            await _client.SendAsync(resolved.SocketPath!,
                new DiagnosticsRequest { Method = "resize", X = driver.Width, Y = driver.Height },
                cancellationToken);
        }

        // Connect to the terminal socket for the attach session
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(resolved.SocketPath!), cancellationToken);
        }
        catch (Exception ex)
        {
            Formatter.WriteError($"Cannot connect: {ex.Message}");
            socket.Dispose();
            return 1;
        }

        await using var stream = new NetworkStream(socket, ownsSocket: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Send attach request
        var request = new DiagnosticsRequest { Method = "attach" };
        var requestJson = JsonSerializer.Serialize(request, DiagnosticsJsonOptions.Default);
        await writer.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

        // Read initial response (contains initial ANSI capture)
        var responseLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(responseLine))
        {
            Formatter.WriteError("Empty response from terminal");
            return 1;
        }

        var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, DiagnosticsJsonOptions.Default);
        if (response is not { Success: true })
        {
            Formatter.WriteError(response?.Error ?? "Attach failed");
            return 1;
        }

        // Write initial screen content before entering raw mode
        if (response.Data != null)
        {
            driver.Write(Encoding.UTF8.GetBytes(response.Data));
            driver.Flush();
        }

        var state = new AttachState { IsLeader = response.Leader == true };

        // Claim leadership if requested
        if (parseResult.GetValue(s_leadOption) && !state.IsLeader)
        {
            await writer.WriteLineAsync("lead".AsMemory(), cancellationToken);
            var leadResponse = await reader.ReadLineAsync(cancellationToken);
            if (leadResponse == "leader:true")
                state.IsLeader = true;
        }

        using var detachCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        driver.EnterRawMode();
        try
        {
            void OnResize(int width, int height)
            {
                if (!state.IsLeader) return;
                try { writer.WriteLine($"r:{width},{height}"); }
                catch { /* best effort */ }
            }
            driver.Resized += OnResize;

            var outputTask = ReadOutputAsync(reader, driver, state, detachCts);
            var inputTask = ReadInputAsync(driver, writer, state, detachCts);

            await Task.WhenAny(outputTask, inputTask);
            await detachCts.CancelAsync();

            driver.Resized -= OnResize;

            try { await Task.WhenAll(outputTask, inputTask); }
            catch (OperationCanceledException) { }
        }
        finally
        {
            var resetSequence =
                Input.MouseParser.DisableMouseTracking +
                "\x1b[?2004l" +  // Disable bracketed paste
                "\x1b[0m" +     // Reset text attributes
                "\x1b[?25h" +   // Show cursor
                "\x1b[?1049l";  // Exit alternate screen
            driver.Write(Encoding.UTF8.GetBytes(resetSequence));
            driver.Flush();

            driver.ExitRawMode();

            if (state.ShutdownRequested)
            {
                try { await writer.WriteLineAsync("shutdown"); } catch { }
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Terminated remote session {resolved.Id}.");
            }
            else
            {
                try { await writer.WriteLineAsync("detach"); } catch { }
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Detached from {resolved.Id}{(state.IsLeader ? " (leader)" : "")}.");
            }
        }

        return 0;
    }

    private static async Task ReadOutputAsync(StreamReader reader, IConsoleDriver driver, AttachState state, CancellationTokenSource detachCts)
    {
        try
        {
            while (!detachCts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(detachCts.Token);
                if (line == null)
                {
                    await detachCts.CancelAsync();
                    return;
                }

                if (line.StartsWith("o:"))
                {
                    var base64 = line[2..];
                    var bytes = Convert.FromBase64String(base64);
                    driver.Write(bytes);
                    driver.Flush();
                }
                else if (line == "exit")
                {
                    await detachCts.CancelAsync();
                    return;
                }
                else if (line == "leader:true")
                {
                    state.IsLeader = true;
                }
                else if (line == "leader:false")
                {
                    state.IsLeader = false;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { try { await detachCts.CancelAsync(); } catch { } }
    }

    private static async Task ReadInputAsync(IConsoleDriver driver, StreamWriter writer, AttachState state, CancellationTokenSource detachCts)
    {
        const byte CtrlRightBracket = 0x1D; // Ctrl+]
        var buffer = new byte[256];

        try
        {
            while (!detachCts.Token.IsCancellationRequested)
            {
                var bytesRead = await driver.ReadAsync(buffer, detachCts.Token);
                if (bytesRead == 0) continue;

                // Scan for Ctrl+] to enter command mode
                var offset = 0;
                for (var i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] != CtrlRightBracket) continue;

                    // Send any bytes before the Ctrl+]
                    if (i > offset)
                    {
                        var pre = Convert.ToBase64String(buffer.AsSpan(offset, i - offset));
                        await writer.WriteLineAsync($"i:{pre}".AsMemory(), detachCts.Token);
                    }

                    // Enter command mode — read next byte for the command
                    var cmd = await ReadCommandByteAsync(driver, buffer, i + 1, bytesRead, detachCts.Token);

                    switch (cmd)
                    {
                        case (byte)'d': // detach
                            await detachCts.CancelAsync();
                            return;

                        case (byte)'l': // toggle leadership
                            await writer.WriteLineAsync("lead".AsMemory(), detachCts.Token);
                            break;

                        case (byte)'q': // quit (shutdown remote)
                            state.ShutdownRequested = true;
                            await detachCts.CancelAsync();
                            return;

                        case CtrlRightBracket: // literal Ctrl+]
                            await writer.WriteLineAsync(
                                $"i:{Convert.ToBase64String([CtrlRightBracket])}".AsMemory(),
                                detachCts.Token);
                            break;

                        default:
                            // Unknown command — ignore, resume session
                            break;
                    }

                    // Skip past the command byte; continue scanning rest of buffer
                    // ReadCommandByteAsync consumed from buffer[i+1..bytesRead] or did a fresh read
                    // Either way, everything up to here is consumed
                    offset = bytesRead; // consumed entire buffer in command mode
                    break; // restart outer loop for fresh reads
                }

                // Send remaining bytes after last scan position
                if (offset < bytesRead)
                {
                    var base64 = Convert.ToBase64String(buffer.AsSpan(offset, bytesRead - offset));
                    await writer.WriteLineAsync($"i:{base64}".AsMemory(), detachCts.Token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { await detachCts.CancelAsync(); }
    }

    /// <summary>
    /// Reads the command byte after Ctrl+]. If there are remaining bytes in the current
    /// buffer (Ctrl+] wasn't the last byte), uses the next byte. Otherwise reads fresh.
    /// </summary>
    private static async Task<byte> ReadCommandByteAsync(
        IConsoleDriver driver, byte[] buffer, int nextIndex, int bytesRead, CancellationToken ct)
    {
        if (nextIndex < bytesRead)
            return buffer[nextIndex];

        // Need a fresh read for the command byte
        var cmdBuf = new byte[1];
        while (!ct.IsCancellationRequested)
        {
            var n = await driver.ReadAsync(cmdBuf, ct);
            if (n > 0) return cmdBuf[0];
        }
        return 0;
    }

    /// <summary>
    /// Mutable state shared between the output and input tasks during an attach session.
    /// </summary>
    private sealed class AttachState
    {
        public volatile bool IsLeader;
        public volatile bool ShutdownRequested;
    }
}
