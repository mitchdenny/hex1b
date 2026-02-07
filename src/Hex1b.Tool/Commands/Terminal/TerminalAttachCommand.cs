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
/// and forwarding local input to it. Ctrl+] to detach.
/// </summary>
internal sealed class TerminalAttachCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<bool> s_resizeOption = new("--resize") { Description = "Resize remote terminal to match local terminal dimensions" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalAttachCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalAttachCommand> logger)
        : base("attach", "Attach to a terminal (Ctrl+] to detach)", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_resizeOption);
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

        using var detachCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        driver.EnterRawMode();
        try
        {
            var outputTask = ReadOutputAsync(reader, driver, detachCts.Token);
            var inputTask = ReadInputAsync(driver, writer, detachCts);

            await Task.WhenAny(outputTask, inputTask);
            await detachCts.CancelAsync();

            try { await Task.WhenAll(outputTask, inputTask); }
            catch (OperationCanceledException) { }
        }
        finally
        {
            // Reset terminal state before exiting raw mode — undo anything the
            // remote terminal's output stream may have enabled (mouse tracking,
            // alternate screen, bracketed paste, etc.)
            var resetSequence =
                Input.MouseParser.DisableMouseTracking +
                "\x1b[?2004l" +  // Disable bracketed paste
                "\x1b[0m" +     // Reset text attributes
                "\x1b[?25h" +   // Show cursor
                "\x1b[?1049l";  // Exit alternate screen
            driver.Write(Encoding.UTF8.GetBytes(resetSequence));
            driver.Flush();

            driver.ExitRawMode();
            // Send detach signal (best effort)
            try { await writer.WriteLineAsync("detach"); } catch { }

            Console.Error.WriteLine();
            Console.Error.WriteLine($"Detached from {resolved.Id}.");
        }

        return 0;
    }

    private static async Task ReadOutputAsync(StreamReader reader, IConsoleDriver driver, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) return;

                if (line.StartsWith("o:"))
                {
                    var base64 = line[2..];
                    var bytes = Convert.FromBase64String(base64);
                    driver.Write(bytes);
                    driver.Flush();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task ReadInputAsync(IConsoleDriver driver, StreamWriter writer, CancellationTokenSource detachCts)
    {
        var buffer = new byte[256];

        try
        {
            while (!detachCts.Token.IsCancellationRequested)
            {
                var bytesRead = await driver.ReadAsync(buffer, detachCts.Token);
                if (bytesRead == 0) continue;

                // Check for Ctrl+] (0x1D) to detach
                for (var i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0x1D)
                    {
                        await detachCts.CancelAsync();
                        return;
                    }
                }

                var base64 = Convert.ToBase64String(buffer.AsSpan(0, bytesRead));
                await writer.WriteLineAsync($"i:{base64}".AsMemory(), detachCts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { await detachCts.CancelAsync(); }
    }
}
