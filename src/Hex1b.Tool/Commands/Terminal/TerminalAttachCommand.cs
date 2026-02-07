using System.CommandLine;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;
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

    private readonly TerminalIdResolver _resolver;

    public TerminalAttachCommand(
        TerminalIdResolver resolver,
        OutputFormatter formatter,
        ILogger<TerminalAttachCommand> logger)
        : base("attach", "Attach to a terminal (Ctrl+] to detach)", formatter, logger)
    {
        _resolver = resolver;

        Arguments.Add(s_idArgument);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        // Connect to the terminal socket
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

        // Write initial screen content
        var stdout = Console.OpenStandardOutput();
        if (response.Data != null)
        {
            var initialBytes = Encoding.UTF8.GetBytes(response.Data);
            await stdout.WriteAsync(initialBytes, cancellationToken);
            await stdout.FlushAsync(cancellationToken);
        }

        Console.Error.Write($"\x1b[s"); // Save cursor for status messages

        using var detachCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Enter raw mode, run I/O loops, restore on exit
        var originalMode = EnterRawMode();
        try
        {
            var outputTask = ReadOutputAsync(reader, stdout, detachCts.Token);
            var inputTask = ReadInputAndForwardAsync(writer, detachCts);

            await Task.WhenAny(outputTask, inputTask);
            await detachCts.CancelAsync();

            try { await Task.WhenAll(outputTask, inputTask); }
            catch (OperationCanceledException) { }
        }
        finally
        {
            RestoreMode(originalMode);
            // Send detach signal (best effort)
            try { await writer.WriteLineAsync("detach"); } catch { }

            // Restore terminal state
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Detached from {resolved.Id}.");
        }

        return 0;
    }

    private static async Task ReadOutputAsync(StreamReader reader, Stream stdout, CancellationToken ct)
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
                    await stdout.WriteAsync(bytes, ct);
                    await stdout.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task ReadInputAndForwardAsync(StreamWriter writer, CancellationTokenSource detachCts)
    {
        var stdin = Console.OpenStandardInput();
        var buffer = new byte[256];

        try
        {
            while (!detachCts.Token.IsCancellationRequested)
            {
                var bytesRead = await stdin.ReadAsync(buffer, detachCts.Token);
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

    // --- Raw mode helpers (POSIX) ---

    private static nint EnterRawMode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return 0;

        try
        {
            // Use stty to save and set raw mode
            var saveProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "stty",
                Arguments = "-g",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            saveProcess?.WaitForExit();
            var saved = saveProcess?.StandardOutput.ReadToEnd().Trim() ?? "";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "stty",
                Arguments = "raw -echo",
                UseShellExecute = false
            })?.WaitForExit();

            // Store the saved settings as a string pointer hack — just use a static field
            s_savedSttySettings = saved;
            return 1; // Indicates we entered raw mode
        }
        catch
        {
            return 0;
        }
    }

    private static string? s_savedSttySettings;

    private static void RestoreMode(nint mode)
    {
        if (mode == 0 || s_savedSttySettings == null) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "stty",
                Arguments = s_savedSttySettings,
                UseShellExecute = false
            })?.WaitForExit();
        }
        catch { }
    }
}
