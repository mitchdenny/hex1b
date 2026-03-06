using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// End-to-end tests for the LSP integration pipeline:
/// TestLanguageServer → JsonRpcTransport → LanguageServerClient → LanguageServerDecorationProvider
/// </summary>
public class LspIntegrationTests : IAsyncLifetime
{
    private TestLanguageServer? _server;
    private LanguageServerClient? _client;

    public async ValueTask InitializeAsync()
    {
        _server = new TestLanguageServer();
        _server.Start();

        var config = new LanguageServerConfiguration
        {
            LanguageId = "csharp",
            DocumentUri = "file:///test.cs",
        };
        config.Transport = new JsonRpcTransport(_server.ClientInput, _server.ClientOutput);

        _client = new LanguageServerClient(config);
        await _client.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            try { await _client.StopAsync(); } catch { }
            await _client.DisposeAsync();
        }
        if (_server != null)
            await _server.DisposeAsync();
    }

    [Fact]
    public void Initialize_ReturnsServerCapabilities()
    {
        Assert.NotNull(_client!.ServerCapabilities);
        Assert.NotNull(_client.ServerCapabilities!.SemanticTokensProvider);
        Assert.NotNull(_client.ServerCapabilities.CompletionProvider);
    }

    [Fact]
    public async Task SemanticTokens_ReturnsTokensForKeywords()
    {
        await _client!.OpenDocumentAsync("public class Foo { }");

        var result = await _client.RequestSemanticTokensAsync();

        Assert.NotNull(result);
        Assert.True(result!.Data.Length > 0, "Should return semantic tokens");

        // "public" starts at line 0, char 0
        // First token group: [deltaLine=0, deltaChar=0, length=6, tokenType=0(keyword), modifiers=0]
        Assert.Equal(0, result.Data[0]); // deltaLine
        Assert.Equal(0, result.Data[1]); // deltaChar
        Assert.Equal(6, result.Data[2]); // length of "public"
        Assert.Equal(0, result.Data[3]); // tokenType = keyword
    }

    [Fact]
    public async Task SemanticTokens_MapsToDecorationSpans()
    {
        var legend = new[] { "keyword", "type", "string", "comment", "number", "variable", "function", "namespace" };
        await _client!.OpenDocumentAsync("public class Foo { }");

        var result = await _client.RequestSemanticTokensAsync();
        var spans = SemanticTokenMapper.MapTokens(result!.Data, legend);

        Assert.True(spans.Count >= 2, "Should have spans for 'public' and 'class'");

        // First span should be "public" at line 1, col 1
        var first = spans[0];
        Assert.Equal(1, first.Start.Line);
        Assert.Equal(1, first.Start.Column);
        Assert.Equal(7, first.End.Column); // "public" = 6 chars, end is exclusive
    }

    [Fact]
    public async Task Diagnostics_ReceivedForTodoPattern()
    {
        var diagnosticsReceived = new TaskCompletionSource<JsonRpcResponse>();
        _client!.NotificationReceived += msg =>
        {
            if (msg.Method == "textDocument/publishDiagnostics")
                diagnosticsReceived.TrySetResult(msg);
        };

        await _client.OpenDocumentAsync("// TODO: fix this");

        var result = await diagnosticsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result.Params);

        var diagParams = System.Text.Json.JsonSerializer.Deserialize<PublishDiagnosticsParams>(
            result.Params!.Value.GetRawText());
        Assert.NotNull(diagParams);
        Assert.Single(diagParams!.Diagnostics);
        Assert.Equal(2, diagParams.Diagnostics[0].Severity); // Warning
        Assert.Contains("TODO", diagParams.Diagnostics[0].Message);
    }

    [Fact]
    public async Task Diagnostics_MapToDecorationSpans()
    {
        var diagnosticsReceived = new TaskCompletionSource<JsonRpcResponse>();
        _client!.NotificationReceived += msg =>
        {
            if (msg.Method == "textDocument/publishDiagnostics")
                diagnosticsReceived.TrySetResult(msg);
        };

        await _client.OpenDocumentAsync("var x = undefinedVar;");

        var result = await diagnosticsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var diagParams = System.Text.Json.JsonSerializer.Deserialize<PublishDiagnosticsParams>(
            result.Params!.Value.GetRawText());

        var spans = DiagnosticMapper.MapDiagnostics(diagParams!.Diagnostics);
        Assert.Single(spans);

        var span = spans[0];
        Assert.Equal(UnderlineStyle.Curly, span.Decoration.UnderlineStyle);
        Assert.Equal(1, span.Start.Line); // LSP line 0 → DocumentPosition line 1
    }

    [Fact]
    public async Task Completion_ReturnsItems()
    {
        await _client!.OpenDocumentAsync("var x = foo.");

        var result = await _client.RequestCompletionAsync(0, 12);

        Assert.NotNull(result);
        Assert.True(result!.Items.Length > 0);
        Assert.Contains(result.Items, i => i.Label == "ToString");
        Assert.Contains(result.Items, i => i.Label == "GetHashCode");
    }

    [Fact]
    public async Task DocumentChange_UpdatesTokens()
    {
        await _client!.OpenDocumentAsync("var x = 1;");
        var result1 = await _client.RequestSemanticTokensAsync();

        await _client.ChangeDocumentAsync("public static void Main() { }");
        var result2 = await _client.RequestSemanticTokensAsync();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Second result should have more tokens (public, static, void)
        Assert.True(result2!.Data.Length > result1!.Data.Length);
    }
}
