using System.Threading.Channels;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Widgets;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// Integration tests for real language servers.
/// Requires typescript-language-server to be installed:
///   npm install -g typescript-language-server typescript
/// </summary>
[TestClass]
public class RealLanguageServerTests
{
    private readonly string _workspacePath;
    public TestContext TestContext { get; set; } = null!;

    public RealLanguageServerTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "LanguageServerDemo", "workspace");
            if (Directory.Exists(candidate))
            {
                _workspacePath = candidate;
                return;
            }
            dir = dir.Parent;
        }
        _workspacePath = "";
    }

    [TestInitialize]
    public Task InitializeAsync() => Task.CompletedTask;
    [TestCleanup]
    public Task DisposeAsync() => Task.CompletedTask;

    private static bool TypeScriptLsAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("typescript-language-server", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    [TestMethod]
    public async Task TypeScriptLs_ReturnsSemanticTokens()
    {
        if (!TypeScriptLsAvailable() || !Directory.Exists(_workspacePath))
            return;

        var config = new LanguageServerConfiguration();
        config.WithServerCommand("typescript-language-server", "--stdio");
        config.WithWorkingDirectory(_workspacePath);
        config.RootUri = "file://" + _workspacePath;
        config.LanguageId = "typescript";

        var client = new LanguageServerClient(config);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await client.StartAsync(cts.Token);
            Assert.IsNotNull(client.ServerCapabilities);

            var filePath = Path.Combine(_workspacePath, "TaskManager.ts");
            var fileUri = "file://" + filePath;
            var text = File.ReadAllText(filePath);

            await client.OpenDocumentAsync(fileUri, "typescript", text, cts.Token);

            // TypeScript LS responds quickly — just wait briefly for project init
            await Task.Delay(2000, cts.Token);

            var tokens = await client.RequestSemanticTokensAsync(fileUri, cts.Token);

            Assert.IsNotNull(tokens);
            Assert.IsTrue(tokens!.Data.Length > 0, "Expected semantic tokens from typescript-language-server");

            // Verify token count is reasonable (TaskManager.ts should have 50+ tokens)
            var tokenCount = tokens.Data.Length / 5;
            Assert.IsTrue(tokenCount > 20, $"Expected at least 20 semantic tokens, got {tokenCount}");

            await client.StopAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task TypeScriptLs_ViaWorkspace_ReturnsTokens()
    {
        await RequireTypeScriptLsAsync();

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var workspace = new Hex1bDocumentWorkspace(_workspacePath);
        try
        {
            workspace.AddLanguageServer("ts-ls", lsp => lsp
                .WithServerCommand("typescript-language-server", "--stdio")
                .WithLanguageId("typescript"));
            workspace.MapLanguageServer("*.ts", "ts-ls");

            var doc = await workspace.OpenDocumentAsync("TaskManager.ts", cancellation.Token);
            var provider = workspace.GetProvider(doc);
            Assert.IsNotNull(provider);
            var session = new TestEditorSession(new EditorState(doc));
            provider.Activate(session);

            IReadOnlyList<TextDecorationSpan> decorations;
            do
            {
                await session.WaitForInvalidationAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
                decorations = provider.GetDecorations(1, doc.LineCount, doc);
            } while (!decorations.Any(span => span.Decoration.ForegroundThemeElement is not null));

            Assert.IsTrue(decorations.Count > 0, "Expected semantic token decorations from typescript-language-server");
            Assert.IsTrue(provider.IsConnected);
        }
        finally
        {
            cancellation.Cancel();
            await Task.Run(async () => await workspace.DisposeAsync()).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private async Task RequireTypeScriptLsAsync()
    {
        Assert.IsTrue(Directory.Exists(_workspacePath), "The tracked LanguageServerDemo workspace must exist.");
        System.Diagnostics.Process? process = null;
        try
        {
            process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "typescript-language-server", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 2)
        {
            Assert.Inconclusive("Requires typescript-language-server and typescript on PATH.");
        }

        Assert.IsNotNull(process);
        using (process)
        using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
        {
            cancellation.CancelAfter(TimeSpan.FromSeconds(5));
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
                var version = await output.WaitAsync(TimeSpan.FromSeconds(5));
                var diagnostics = await error.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(0, process.ExitCode, $"TypeScript LS version probe failed: {diagnostics}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(version), "Expected a TypeScript LS version.");
                TestContext.WriteLine($"TypeScript LS version: {version.Trim()}");
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    [TestMethod]
    public async Task TypeScriptLs_ReturnsCompletions()
    {
        if (!TypeScriptLsAvailable() || !Directory.Exists(_workspacePath))
            return;

        var config = new LanguageServerConfiguration();
        config.WithServerCommand("typescript-language-server", "--stdio");
        config.WithWorkingDirectory(_workspacePath);
        config.RootUri = "file://" + _workspacePath;
        config.LanguageId = "typescript";

        var client = new LanguageServerClient(config);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await client.StartAsync(cts.Token);

            var filePath = Path.Combine(_workspacePath, "TaskManager.ts");
            var fileUri = "file://" + filePath;
            var text = File.ReadAllText(filePath);

            await client.OpenDocumentAsync(fileUri, "typescript", text, cts.Token);
            await Task.Delay(2000, cts.Token);

            // Request completions after "this." on line that has it
            // Find a line with "this." and request at its position
            var lines = text.Split('\n');
            var line = -1;
            var col = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                var idx = lines[i].IndexOf("this.", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    line = i; // 0-based
                    col = idx + 5; // After "this."
                    break;
                }
            }

            Assert.IsTrue(line >= 0, "Expected 'this.' in TaskManager.ts");

            var completions = await client.RequestCompletionAsync(fileUri, line, col, ct: cts.Token);
            Assert.IsNotNull(completions);
            Assert.IsTrue(completions!.Items.Length > 0, "Expected completions after 'this.'");

            // Should contain class members
            var labels = completions.Items.Select(i => i.Label).ToList();
            Assert.IsTrue(labels.Any(l => !string.IsNullOrEmpty(l)));

            await client.StopAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    private sealed class TestEditorSession : IEditorSession
    {
        private readonly Channel<byte> _invalidations = Channel.CreateUnbounded<byte>();
        public TestEditorSession(EditorState state) => State = state;
        public EditorState State { get; }
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public void Invalidate() => _invalidations.Writer.TryWrite(0);
        public ValueTask<byte> WaitForInvalidationAsync(CancellationToken ct) => _invalidations.Reader.ReadAsync(ct);
        public void PushOverlay(EditorOverlay overlay) { }
        public void DismissOverlay(string id) { }
        public IReadOnlyList<EditorOverlay> ActiveOverlays => [];

        public void PushInlineHints(IReadOnlyList<InlineHint> hints) { }
        public void ClearInlineHints() { }
        public IReadOnlyList<InlineHint> ActiveInlineHints => [];
        public void PushRangeHighlights(IReadOnlyList<RangeHighlight> highlights) { }
        public void ClearRangeHighlights() { }
        public IReadOnlyList<RangeHighlight> ActiveRangeHighlights => [];
        public void PushGutterDecorations(IReadOnlyList<GutterDecoration> decorations) { }
        public void ClearGutterDecorations() { }
        public IReadOnlyList<GutterDecoration> ActiveGutterDecorations => [];
        public void SetFoldingRegions(IReadOnlyList<FoldingRegion> regions) { }
        public IReadOnlyList<FoldingRegion> FoldingRegions => [];
        public void SetBreadcrumbs(BreadcrumbData? data) { }
        public BreadcrumbData? Breadcrumbs => null;
        public Task<string?> ShowActionMenuAsync(ActionMenu menu) => Task.FromResult<string?>(null);
        public void ShowSignaturePanel(SignaturePanel panel) { }
        public void DismissSignaturePanel() { }
    }
}
