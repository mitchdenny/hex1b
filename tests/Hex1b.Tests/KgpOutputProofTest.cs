// Add to KgpPipelineDiagnostic.cs - proves KGP bytes reach presentation after alt screen
using System.Collections.Concurrent;
using System.Text;
using Hex1b;
using Hex1b.Widgets;
using Hex1b.Tokens;
using Xunit;

namespace Hex1b.Tests;

public class KgpOutputProofTest
{
    [Fact]
    public async Task Hex1bApp_KgpWidget_KgpBytesReachOutputAfterAltScreen()
    {
        // This test proves whether KGP escape sequences make it to the
        // workload output AFTER the app enters alt screen mode.
        
        var pixelData = new byte[4 * 4 * 4]; // 4x4 red image
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = 255;     // R
            pixelData[i + 3] = 255; // A
        }

        var capabilities = new TerminalCapabilities
        {
            SupportsMouse = false,
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(40, 20);

        var options = new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
            EnableMouse = false,
        };

        // Collect ALL output items
        var allOutput = new ConcurrentQueue<(byte[] Bytes, IReadOnlyList<AnsiToken>? Tokens)>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty)
                    {
                        allOutput.Enqueue((item.Bytes.ToArray(), item.Tokens));
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(ctx =>
            ctx.VStack(v => [
                v.Text("KGP Test"),
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2)
            ]),
            options);

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
        try { await app.RunAsync(appCts.Token); }
        catch (OperationCanceledException) { }
        await app.DisposeAsync();

        await Task.Delay(200);
        readCts.Cancel();
        try { await readTask; } catch { }

        // Analyze output
        var sb = new StringBuilder();
        var totalItems = allOutput.Count;
        var altScreenSeen = false;
        var kgpAfterAltScreen = false;
        var kgpBeforeAltScreen = false;
        int itemIndex = 0;

        foreach (var (bytes, tokens) in allOutput)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var hasAltScreen = text.Contains("\x1b[?1049h");
            var hasKgp = text.Contains("\x1b_G");

            if (hasAltScreen) altScreenSeen = true;
            if (hasKgp && !altScreenSeen) kgpBeforeAltScreen = true;
            if (hasKgp && altScreenSeen) kgpAfterAltScreen = true;

            if (hasAltScreen || hasKgp)
            {
                sb.AppendLine($"Item {itemIndex}: altScreen={hasAltScreen}, kgp={hasKgp}, bytes={bytes.Length}");
                if (hasKgp)
                {
                    // Find the KGP sequence start
                    var kgpStart = text.IndexOf("\x1b_G");
                    var kgpPreview = text.Substring(kgpStart, Math.Min(80, text.Length - kgpStart));
                    sb.AppendLine($"  KGP preview: {kgpPreview.Replace("\x1b", "ESC")}");
                }
                if (tokens != null)
                {
                    var kgpTokens = tokens.Where(t => t is KgpToken || 
                        (t is UnrecognizedSequenceToken u && u.Sequence.Contains("\x1b_G"))).ToList();
                    sb.AppendLine($"  Pre-tokenized: {tokens.Count} tokens, {kgpTokens.Count} KGP-related");
                    foreach (var t in kgpTokens)
                        sb.AppendLine($"    Token type: {t.GetType().Name}");
                }
            }
            itemIndex++;
        }

        sb.AppendLine($"\nSummary: {totalItems} output items, altScreenSeen={altScreenSeen}, kgpBeforeAlt={kgpBeforeAltScreen}, kgpAfterAlt={kgpAfterAltScreen}");

        // Write diagnostics to file for inspection
        var diagnostics = sb.ToString();
        File.WriteAllText("/tmp/kgp-proof-output.txt", diagnostics);
        
        // The critical assertion: KGP bytes must appear AFTER alt screen
        Assert.True(kgpAfterAltScreen, 
            $"KGP escape sequences did NOT appear in output after alt screen entry.\n{diagnostics}");
    }

    [Fact]
    public async Task KgpSequence_IsWellFormed_AndFollowsAltScreen()
    {
        // This test verifies the EXACT byte ordering and KGP sequence structure
        // that a real terminal would receive.
        
        var pixelData = new byte[4 * 4 * 4]; // 4x4 red image
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = 255;     // R
            pixelData[i + 3] = 255; // A
        }

        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(40, 20);

        var allBytes = new List<byte>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty)
                        allBytes.AddRange(item.Bytes.ToArray());
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(ctx =>
            ctx.VStack(v => [
                v.Text("Test"),
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = false });

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await app.RunAsync(appCts.Token); } catch (OperationCanceledException) { }
        await app.DisposeAsync();
        await Task.Delay(100);
        readCts.Cancel();
        try { await readTask; } catch { }

        var text = Encoding.UTF8.GetString(allBytes.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine($"Total bytes: {allBytes.Count}");

        // Find key sequences and their positions
        var altScreenPos = text.IndexOf("\x1b[?1049h");
        var clearScreenPos = text.IndexOf("\x1b[2J");
        sb.AppendLine($"Alt screen at byte offset: {altScreenPos}");
        sb.AppendLine($"Clear screen at byte offset: {clearScreenPos}");

        // Find all KGP sequences
        var kgpPositions = new List<(int Position, string Header, int Length)>();
        int pos = 0;
        while ((pos = text.IndexOf("\x1b_G", pos)) >= 0)
        {
            var end = text.IndexOf("\x1b\\", pos);
            if (end < 0) { sb.AppendLine($"KGP at {pos}: UNTERMINATED!"); break; }
            var seq = text.Substring(pos, end - pos + 2);
            var semiPos = seq.IndexOf(';');
            var header = semiPos >= 0 ? seq[2..semiPos] : seq[2..^2];
            kgpPositions.Add((pos, header, seq.Length));
            sb.AppendLine($"KGP at byte {pos}: header=\"{header}\", seq_len={seq.Length}");
            pos = end + 2;
        }

        // Check cursor position before KGP
        foreach (var (kgpPos, header, _) in kgpPositions)
        {
            // Look backwards for the nearest CUP sequence (\x1b[row;colH)
            var searchStart = Math.Max(0, kgpPos - 20);
            var before = text.Substring(searchStart, kgpPos - searchStart);
            var cupMatch = System.Text.RegularExpressions.Regex.Match(before, @"\x1b\[(\d+)(?:;(\d+))?H");
            if (cupMatch.Success)
            {
                var col = cupMatch.Groups[2].Success ? cupMatch.Groups[2].Value : "1";
                sb.AppendLine($"  Cursor before KGP: row={cupMatch.Groups[1].Value}, col={col}");
            }
            else
            {
                sb.AppendLine($"  WARNING: No cursor position found before KGP!");
            }
        }

        var diagnostics = sb.ToString();
        File.WriteAllText("/tmp/kgp-byte-analysis.txt", diagnostics);
        
        // Dump full escaped output for debugging
        var escapedFull = new StringBuilder();
        foreach (char c in text)
        {
            if (c == '\x1b') escapedFull.Append("ESC");
            else if (c < 0x20 && c != '\n') escapedFull.Append($"<{(int)c:X2}>");
            else escapedFull.Append(c);
        }
        // Truncate base64 data for readability
        var escaped = escapedFull.ToString();
        var dataStart = escaped.IndexOf(";/");
        if (dataStart > 0)
        {
            var dataEnd = escaped.IndexOf("ESC\\", dataStart);
            if (dataEnd > 0 && dataEnd - dataStart > 40)
                escaped = escaped[..dataStart] + ";[BASE64_DATA]" + escaped[dataEnd..];
        }
        File.AppendAllText("/tmp/kgp-byte-analysis.txt", "\n\nFull output (escaped):\n" + escaped);

        // Assertions
        Assert.True(altScreenPos >= 0, "Alt screen sequence not found");
        Assert.True(kgpPositions.Count > 0, $"No KGP sequences found!\n{diagnostics}");
        Assert.True(kgpPositions[0].Position > altScreenPos,
            $"KGP appears BEFORE alt screen!\n{diagnostics}");
        
        // Verify KGP header is well-formed
        var firstHeader = kgpPositions[0].Header;
        Assert.Contains("a=T", firstHeader); // transmit+display
        Assert.Contains("f=32", firstHeader); // RGBA format
        Assert.Contains("s=4", firstHeader);  // width
        Assert.Contains("v=4", firstHeader);  // height
        Assert.Contains("i=", firstHeader);   // image ID present
    }

    [Fact]
    public async Task KgpLargeImage_UsesChunkedTransmission()
    {
        // 32x32 RGBA = 4096 bytes = ~5464 base64 bytes, exceeding the 4096 byte chunk limit
        var pixelData = new byte[32 * 32 * 4];
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = 255;     // R
            pixelData[i + 3] = 255; // A
        }

        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(80, 24);

        var allBytes = new List<byte>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty)
                        allBytes.AddRange(item.Bytes.ToArray());
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(ctx =>
            ctx.VStack(v => [
                v.Text("Test"),
                v.KittyGraphics(pixelData, 32, 32).WithDisplaySize(16, 8)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = false });

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await app.RunAsync(appCts.Token); } catch (OperationCanceledException) { }
        await app.DisposeAsync();
        await Task.Delay(100);
        readCts.Cancel();
        try { await readTask; } catch { }

        var text = Encoding.UTF8.GetString(allBytes.ToArray());

        // Count KGP sequences — should have multiple chunks (m=1 and m=0)
        var kgpCount = 0;
        var hasM1 = false;
        var hasM0 = false;
        int pos = 0;
        while ((pos = text.IndexOf("\x1b_G", pos)) >= 0)
        {
            var end = text.IndexOf("\x1b\\", pos);
            if (end < 0) break;
            var seq = text.Substring(pos, end - pos + 2);
            var semiPos = seq.IndexOf(';');
            var header = semiPos >= 0 ? seq[2..semiPos] : seq[2..^2];
            
            if (header.Contains("m=1")) hasM1 = true;
            if (header.Contains("m=0")) hasM0 = true;
            kgpCount++;
            pos = end + 2;
        }

        Assert.True(kgpCount >= 2, $"Expected at least 2 KGP chunks for 32x32 image, got {kgpCount}");
        Assert.True(hasM1, "Expected m=1 (continuation) chunk");
        Assert.True(hasM0, "Expected m=0 (final) chunk");
    }

    [Fact]
    public async Task FullTerminal_KgpWidget_KgpBytesReachPresentation()
    {
        // This test goes through the FULL Hex1bTerminal pipeline (WithHex1bApp path)
        // and captures what the presentation adapter actually receives.
        
        var pixelData = new byte[4 * 4 * 4]; // 4x4 red image
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = 255;     // R
            pixelData[i + 3] = 255; // A
        }

        var receivedBytes = new ConcurrentBag<byte[]>();
        
        // Use a spy presentation adapter
        var spy = new SpyPresentationAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
                v.Text("KGP Test"),
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2)
            ]))
            .WithPresentation(spy)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));
        try { await terminal.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Analyze what the presentation adapter received
        var sb = new StringBuilder();
        var altScreenSeen = false;
        var kgpAfterAltScreen = false;
        int itemIndex = 0;

        foreach (var bytes in spy.ReceivedOutput)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var hasAltScreen = text.Contains("\x1b[?1049h");
            var hasKgp = text.Contains("\x1b_G");

            if (hasAltScreen) altScreenSeen = true;
            if (hasKgp && altScreenSeen) kgpAfterAltScreen = true;

            if (hasAltScreen || hasKgp)
            {
                sb.AppendLine($"Item {itemIndex}: altScreen={hasAltScreen}, kgp={hasKgp}, bytes={bytes.Length}");
                if (hasKgp)
                {
                    var kgpStart = text.IndexOf("\x1b_G");
                    var kgpPreview = text.Substring(kgpStart, Math.Min(100, text.Length - kgpStart));
                    sb.AppendLine($"  KGP: {kgpPreview.Replace("\x1b", "ESC")}");
                }
            }
            itemIndex++;
        }

        sb.AppendLine($"\nSummary: {spy.ReceivedOutput.Count} writes, altScreenSeen={altScreenSeen}, kgpAfterAlt={kgpAfterAltScreen}");
        sb.AppendLine($"Terminal KGP placements: {terminal.KgpPlacements.Count}");

        var diagnostics = sb.ToString();
        File.WriteAllText("/tmp/kgp-proof-full.txt", diagnostics);

        Assert.True(kgpAfterAltScreen,
            $"KGP bytes did NOT reach presentation adapter after alt screen.\n{diagnostics}");
    }

    /// <summary>Spy presentation adapter that records all output.</summary>
    private class SpyPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        public List<byte[]> ReceivedOutput { get; } = new();

        public TerminalCapabilities Capabilities => new()
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
            SupportsAlternateScreen = true,
            HandlesAlternateScreenNatively = false,
        };

        public int Width => 40;
        public int Height => 20;

        #pragma warning disable CS0067
        public event Action<int, int>? Resized;
        public event Action? Disconnected;
        #pragma warning restore CS0067

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            ReceivedOutput.Add(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            return new ValueTask<ReadOnlyMemory<byte>>(
                Task.Delay(Timeout.Infinite, ct).ContinueWith<ReadOnlyMemory<byte>>(_ => ReadOnlyMemory<byte>.Empty));
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
