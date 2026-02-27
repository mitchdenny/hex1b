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
