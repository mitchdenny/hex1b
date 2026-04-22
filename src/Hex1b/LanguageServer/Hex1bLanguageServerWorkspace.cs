using System.Text.Json;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.LanguageServer;

/// <summary>
/// Manages language server instances for a workspace root directory.
/// Multiple documents in the same workspace share the same server connection
/// per language. Documents register with the workspace and receive a
/// <see cref="LanguageServerDecorationProvider"/> that uses the shared connection.
/// </summary>
/// <remarks>
/// <para>
/// In LSP, a single server process handles all documents in a workspace via
/// <c>textDocument/didOpen</c> and <c>textDocument/didChange</c> notifications.
/// This class manages that shared lifecycle — one <see cref="LanguageServerClient"/>
/// per registered language, shared across all documents of that language.
/// </para>
/// <para>
/// Usage:
/// <code>
/// var workspace = new Hex1bLanguageServerWorkspace("/path/to/project");
/// workspace.RegisterServer("csharp", config => config
///     .WithServerCommand("csharp-ls")
///     .EnableSemanticHighlighting());
///
/// // In the widget builder:
/// ctx.Editor(state).LanguageServer(workspace, "file:///path/to/file.cs", "csharp")
/// </code>
/// </para>
/// </remarks>
public sealed class Hex1bLanguageServerWorkspace : IAsyncDisposable
{
    private readonly string _rootUri;
    private readonly Dictionary<string, LanguageServerConfiguration> _serverConfigs = new();
    private readonly Dictionary<string, LanguageServerClient> _activeClients = new();
    private readonly Dictionary<string, string[]> _tokenLegends = new();
    private readonly Dictionary<string, LanguageServerDecorationProvider> _documentProviders = new();
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    /// <summary>
    /// Creates a workspace rooted at the specified path.
    /// </summary>
    /// <param name="rootPath">
    /// The workspace root directory path. Converted to a <c>file://</c> URI
    /// for the LSP <c>initialize</c> request's <c>rootUri</c> parameter.
    /// </param>
    public Hex1bLanguageServerWorkspace(string rootPath)
    {
        _rootUri = rootPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? rootPath
            : "file://" + rootPath;
    }

    /// <summary>
    /// Registers a language server configuration for a given language ID.
    /// The server is not started until a document of this language is opened.
    /// </summary>
    /// <param name="languageId">LSP language identifier (e.g., "csharp", "python", "typescript").</param>
    /// <param name="configure">Configuration callback.</param>
    public void RegisterServer(string languageId, Action<LanguageServerConfiguration> configure)
    {
        var config = new LanguageServerConfiguration { RootUri = _rootUri };
        configure(config);
        config.LanguageId = languageId;
        _serverConfigs[languageId] = config;
    }

    /// <summary>
    /// Gets or creates a decoration provider for a document, backed by the
    /// shared language server for that language. Call this from the
    /// <see cref="LanguageServerExtensions.LanguageServer(Hex1b.Widgets.EditorWidget, Hex1bLanguageServerWorkspace, string, string?)"/>
    /// extension method.
    /// </summary>
    /// <param name="documentUri">The document's URI (e.g., <c>file:///path/to/file.cs</c>).</param>
    /// <param name="languageId">
    /// The language ID. If null, inferred from the document URI extension.
    /// </param>
    /// <returns>A decoration provider wired to the shared server connection.</returns>
    public LanguageServerDecorationProvider GetProvider(string documentUri, string? languageId = null)
    {
        languageId ??= InferLanguageId(documentUri);

        // Return existing provider for this exact document
        if (_documentProviders.TryGetValue(documentUri, out var existing))
            return existing;

        // Get or create the shared client for this language
        var client = GetOrCreateClient(languageId);
        var legend = _tokenLegends.GetValueOrDefault(languageId) ?? SemanticTokenTypes.All;

        var provider = new LanguageServerDecorationProvider(client, documentUri, languageId, legend);
        _documentProviders[documentUri] = provider;
        return provider;
    }

    private LanguageServerClient GetOrCreateClient(string languageId)
    {
        if (_activeClients.TryGetValue(languageId, out var existing))
            return existing;

        if (!_serverConfigs.TryGetValue(languageId, out var config))
            throw new InvalidOperationException(
                $"No language server registered for language '{languageId}'. " +
                $"Call RegisterServer(\"{languageId}\", ...) first.");

        var client = new LanguageServerClient(config);
        _activeClients[languageId] = client;

        // Start the client in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await client.StartAsync().ConfigureAwait(false);

                // Extract token legend
                if (client.ServerCapabilities?.SemanticTokensProvider != null)
                {
                    try
                    {
                        var provider = client.ServerCapabilities.SemanticTokensProvider.Value;
                        if (provider.TryGetProperty("legend", out var legend) &&
                            legend.TryGetProperty("tokenTypes", out var types))
                        {
                            _tokenLegends[languageId] = JsonSerializer.Deserialize(types.GetRawText(), LspJsonContext.Default.StringArray)
                                ?? SemanticTokenTypes.All;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LSP workspace: failed to start {languageId} server: {ex.Message}");
            }
        });

        return client;
    }

    private static string InferLanguageId(string documentUri)
    {
        var ext = Path.GetExtension(documentUri).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".tsx" => "typescriptreact",
            ".js" => "javascript",
            ".jsx" => "javascriptreact",
            ".py" => "python",
            ".rs" => "rust",
            ".go" => "go",
            ".c" or ".h" => "c",
            ".cpp" or ".cc" or ".cxx" or ".hpp" => "cpp",
            ".java" => "java",
            ".rb" => "ruby",
            ".lua" => "lua",
            ".sh" or ".bash" => "shellscript",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".xml" => "xml",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".md" => "markdown",
            ".sql" => "sql",
            _ => "plaintext",
        };
    }

    /// <summary>Disposes all active server connections.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _documentProviders.Values)
            await provider.DisposeAsync().ConfigureAwait(false);

        foreach (var client in _activeClients.Values)
        {
            try { await client.StopAsync().ConfigureAwait(false); } catch { }
            await client.DisposeAsync().ConfigureAwait(false);
        }

        _documentProviders.Clear();
        _activeClients.Clear();
        _clientLock.Dispose();
    }
}
