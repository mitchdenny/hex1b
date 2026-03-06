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
public class RealLanguageServerTests : IAsyncLifetime
{
    private readonly string _workspacePath;

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

    public ValueTask InitializeAsync() => default;
    public ValueTask DisposeAsync() => default;

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

    [Fact]
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
            Assert.NotNull(client.ServerCapabilities);

            var filePath = Path.Combine(_workspacePath, "TaskManager.ts");
            var fileUri = "file://" + filePath;
            var text = File.ReadAllText(filePath);

            await client.OpenDocumentAsync(fileUri, "typescript", text, cts.Token);

            // TypeScript LS responds quickly — just wait briefly for project init
            await Task.Delay(2000, cts.Token);

            var tokens = await client.RequestSemanticTokensAsync(fileUri, cts.Token);

            Assert.NotNull(tokens);
            Assert.True(tokens!.Data.Length > 0, "Expected semantic tokens from typescript-language-server");

            // Verify token count is reasonable (TaskManager.ts should have 50+ tokens)
            var tokenCount = tokens.Data.Length / 5;
            Assert.True(tokenCount > 20, $"Expected at least 20 semantic tokens, got {tokenCount}");

            await client.StopAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task TypeScriptLs_ViaWorkspace_ReturnsTokens()
    {
        if (!TypeScriptLsAvailable() || !Directory.Exists(_workspacePath))
            return;

        await using var workspace = new Hex1bDocumentWorkspace(_workspacePath);
        workspace.AddLanguageServer("ts-ls", lsp => lsp
            .WithServerCommand("typescript-language-server", "--stdio")
            .WithLanguageId("typescript"));
        workspace.MapLanguageServer("*.ts", "ts-ls");

        var doc = await workspace.OpenDocumentAsync("TaskManager.ts");
        var provider = workspace.GetProvider(doc);
        Assert.NotNull(provider);

        // Activate the provider with a minimal session
        var state = new EditorState(doc);
        var session = new TestEditorSession(state);
        provider.Activate(session);

        // Wait for typescript-language-server to load and return tokens
        var deadline = DateTime.UtcNow.AddSeconds(15);
        IReadOnlyList<TextDecorationSpan>? decorations = null;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000);
            decorations = provider.GetDecorations(1, doc.LineCount, doc);
            if (decorations.Count > 0)
                break;
        }

        Assert.NotNull(decorations);
        Assert.True(decorations.Count > 0, "Expected semantic token decorations from typescript-language-server");

        provider.Deactivate();
    }

    [Fact]
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

            Assert.True(line >= 0, "Expected 'this.' in TaskManager.ts");

            var completions = await client.RequestCompletionAsync(fileUri, line, col, cts.Token);
            Assert.NotNull(completions);
            Assert.True(completions!.Items.Length > 0, "Expected completions after 'this.'");

            // Should contain class members
            var labels = completions.Items.Select(i => i.Label).ToList();
            Assert.Contains(labels, l => !string.IsNullOrEmpty(l));

            await client.StopAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    private sealed class TestEditorSession : IEditorSession
    {
        public TestEditorSession(EditorState state) => State = state;
        public EditorState State { get; }
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public bool Invalidated { get; private set; }
        public void Invalidate() => Invalidated = true;
        public void PushOverlay(EditorOverlay overlay) { }
        public void DismissOverlay(string id) { }
        public IReadOnlyList<EditorOverlay> ActiveOverlays => [];
    }
}
