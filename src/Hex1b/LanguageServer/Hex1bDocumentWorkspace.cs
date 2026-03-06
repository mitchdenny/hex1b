using System.Text.Json;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.LanguageServer;

/// <summary>
/// A workspace rooted at a directory that manages documents and language server connections.
/// </summary>
/// <remarks>
/// <para>
/// Documents can be created in-memory or opened from files. File-backed documents
/// track dirty state and support <see cref="Documents.IHex1bDocument.SaveAsync"/>.
/// </para>
/// <para>
/// Language servers are registered with <see cref="AddLanguageServer"/> and mapped
/// to file patterns with <see cref="MapLanguageServer"/>. When a document is opened,
/// the workspace resolves the appropriate server based on the document's URI.
/// Multiple documents of the same language share a single server connection.
/// </para>
/// <para>
/// Usage:
/// <code>
/// var workspace = new Hex1bDocumentWorkspace("/my/project");
///
/// // Register and map a language server
/// workspace.AddLanguageServer("csharp-ls", lsp => lsp.WithServerCommand("csharp-ls"));
/// workspace.MapLanguageServer("*.cs", "csharp-ls");
///
/// // Open a file-backed document
/// var doc = await workspace.OpenDocumentAsync("src/Program.cs");
/// var state = new EditorState(doc);
///
/// // In the widget builder — workspace resolves the right server:
/// ctx.Editor(state).LanguageServer(workspace)
///
/// // Save changes
/// await doc.SaveAsync();
/// </code>
/// </para>
/// </remarks>
public sealed class Hex1bDocumentWorkspace : IAsyncDisposable
{
    private readonly string _rootPath;
    private readonly string _rootUri;
    private readonly Dictionary<string, LanguageServerConfiguration> _serverConfigs = new();
    private readonly Dictionary<string, LanguageServerClient> _activeClients = new();
    private readonly Dictionary<string, string[]> _tokenLegends = new();
    private readonly List<(string Glob, string ServerId)> _serverMappings = [];
    private readonly Dictionary<string, Func<Documents.ITextDecorationProvider>> _inProcessProviderFactories = new();
    private readonly List<(string Glob, string ProviderId)> _inProcessMappings = [];
    private readonly Dictionary<string, Documents.Hex1bDocument> _openDocuments = new();
    private readonly Dictionary<string, LanguageServerDecorationProvider> _documentProviders = new();
    private readonly Dictionary<string, Documents.ITextDecorationProvider> _inProcessDocumentProviders = new();

    /// <summary>
    /// Creates a workspace rooted at the specified directory path.
    /// </summary>
    /// <param name="rootPath">The workspace root directory. Documents are resolved relative to this path.</param>
    public Hex1bDocumentWorkspace(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _rootUri = "file://" + _rootPath;
    }

    /// <summary>The workspace root directory path.</summary>
    public string RootPath => _rootPath;

    // ── Document management ──────────────────────────────────

    /// <summary>
    /// Opens an existing file as a document. The document is associated with the
    /// file path, tracks dirty state, and supports <see cref="Documents.IHex1bDocument.SaveAsync"/>.
    /// </summary>
    /// <param name="relativePath">Path relative to the workspace root.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A file-backed document.</returns>
    public async Task<Documents.Hex1bDocument> OpenDocumentAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var uri = PathToUri(fullPath);

        if (_openDocuments.TryGetValue(uri, out var existing))
            return existing;

        var bytes = await File.ReadAllBytesAsync(fullPath, ct).ConfigureAwait(false);
        var doc = new Documents.Hex1bDocument(bytes);
        doc.FilePath = fullPath;

        _openDocuments[uri] = doc;
        return doc;
    }

    /// <summary>
    /// Creates a new in-memory document. If <paramref name="relativePath"/> is provided,
    /// the document is associated with that file path (for future saves) but the file
    /// is not created on disk until <see cref="Documents.IHex1bDocument.SaveAsync"/> is called.
    /// </summary>
    /// <param name="initialContent">Initial text content.</param>
    /// <param name="relativePath">Optional path relative to the workspace root.</param>
    /// <returns>A document, optionally file-backed.</returns>
    public Documents.Hex1bDocument CreateDocument(string initialContent = "", string? relativePath = null)
    {
        var doc = new Documents.Hex1bDocument(initialContent);

        if (relativePath != null)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
            doc.FilePath = fullPath;

            var uri = PathToUri(fullPath);
            _openDocuments[uri] = doc;
        }

        return doc;
    }

    /// <summary>
    /// Saves all documents with unsaved changes.
    /// </summary>
    public async Task SaveAllAsync(CancellationToken ct = default)
    {
        foreach (var doc in _openDocuments.Values)
        {
            if (doc.IsDirty)
                await doc.SaveAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Returns all currently open documents.</summary>
    public IReadOnlyCollection<Documents.Hex1bDocument> OpenDocuments => _openDocuments.Values;

    /// <summary>Returns all documents with unsaved changes.</summary>
    public IEnumerable<Documents.Hex1bDocument> DirtyDocuments =>
        _openDocuments.Values.Where(d => d.IsDirty);

    /// <summary>
    /// Returns all current diagnostics aggregated across all open documents
    /// that have connected language servers, ordered by severity then file.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> GetAllDiagnostics()
    {
        var all = new List<DiagnosticInfo>();
        foreach (var provider in _documentProviders.Values)
        {
            all.AddRange(provider.CurrentDiagnostics);
        }
        all.Sort((a, b) =>
        {
            var sev = a.Severity.CompareTo(b.Severity);
            if (sev != 0) return sev;
            var file = string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            if (file != 0) return file;
            return a.Start.Line.CompareTo(b.Start.Line);
        });
        return all;
    }

    /// <summary>
    /// Returns a summary of diagnostic counts by severity across all open documents.
    /// </summary>
    public (int Errors, int Warnings, int Info) GetDiagnosticSummary()
    {
        int errors = 0, warnings = 0, info = 0;
        foreach (var provider in _documentProviders.Values)
        {
            foreach (var diag in provider.CurrentDiagnostics)
            {
                switch (diag.Severity)
                {
                    case DiagnosticSeverity.Error: errors++; break;
                    case DiagnosticSeverity.Warning: warnings++; break;
                    default: info++; break;
                }
            }
        }
        return (errors, warnings, info);
    }

    // ── Language server management ───────────────────────────

    /// <summary>
    /// Registers a language server configuration with a logical name.
    /// The server is not started until a matching document is opened.
    /// </summary>
    /// <param name="serverId">A logical name for this server (e.g., "csharp-ls", "pyright").</param>
    /// <param name="configure">Configuration callback.</param>
    public void AddLanguageServer(string serverId, Action<LanguageServerConfiguration> configure)
    {
        var config = new LanguageServerConfiguration
        {
            RootUri = _rootUri,
            WorkingDirectory = _rootPath,
        };
        configure(config);
        _serverConfigs[serverId] = config;
    }

    /// <summary>
    /// Maps a file glob pattern to a registered language server.
    /// When a document matching the glob is opened, it automatically gets
    /// decorations from the associated language server.
    /// </summary>
    /// <param name="glob">
    /// A glob pattern to match document file names (e.g., "*.cs", "*.{ts,tsx}", "Dockerfile").
    /// Matched against the file name only, not the full path.
    /// </param>
    /// <param name="serverId">The server ID registered with <see cref="AddLanguageServer"/>.</param>
    public void MapLanguageServer(string glob, string serverId)
    {
        if (!_serverConfigs.ContainsKey(serverId))
            throw new InvalidOperationException(
                $"No language server registered with ID '{serverId}'. " +
                $"Call AddLanguageServer(\"{serverId}\", ...) first.");

        _serverMappings.Add((glob, serverId));
    }

    // ── In-process decoration providers ──────────────────────

    /// <summary>
    /// Registers an in-process decoration provider factory with a logical name.
    /// In-process providers run without an external language server process — ideal
    /// for simple syntax highlighting (e.g., diff files, log files, config files).
    /// </summary>
    /// <param name="providerId">A logical name for this provider (e.g., "git-diff", "log-highlighter").</param>
    /// <param name="factory">A factory that creates a new provider instance per document.</param>
    public void AddDecorationProvider(string providerId, Func<Documents.ITextDecorationProvider> factory)
    {
        _inProcessProviderFactories[providerId] = factory;
    }

    /// <summary>
    /// Maps a file glob pattern to a registered in-process decoration provider.
    /// In-process mappings are checked before language server mappings.
    /// </summary>
    /// <param name="glob">
    /// A glob pattern to match document file names (e.g., "*.diff", "*.patch", "*.log").
    /// Matched against the file name only, not the full path.
    /// </param>
    /// <param name="providerId">The provider ID registered with <see cref="AddDecorationProvider"/>.</param>
    public void MapDecorationProvider(string glob, string providerId)
    {
        if (!_inProcessProviderFactories.ContainsKey(providerId))
            throw new InvalidOperationException(
                $"No decoration provider registered with ID '{providerId}'. " +
                $"Call AddDecorationProvider(\"{providerId}\", ...) first.");

        _inProcessMappings.Add((glob, providerId));
    }

    /// <summary>
    /// Gets a decoration provider for a document, using the workspace's
    /// language server mappings to resolve the appropriate server.
    /// Returns null if no server is mapped for this document type.
    /// </summary>
    /// <param name="document">The document to get a provider for.</param>
    /// <returns>A decoration provider, or null if no server matches.</returns>
    public LanguageServerDecorationProvider? GetProvider(Documents.IHex1bDocument document)
    {
        var filePath = document.FilePath;
        if (filePath == null) return null;

        var uri = PathToUri(filePath);
        if (_documentProviders.TryGetValue(uri, out var existing))
            return existing;

        // Find matching server
        var serverId = ResolveServer(filePath);
        if (serverId == null) return null;

        var config = _serverConfigs[serverId];
        var languageId = config.LanguageId ?? InferLanguageId(filePath);
        var client = GetOrCreateClient(serverId, config);
        var legend = _tokenLegends.GetValueOrDefault(serverId) ?? SemanticTokenTypes.All;

        var provider = new LanguageServerDecorationProvider(client, uri, languageId, legend);
        _documentProviders[uri] = provider;
        return provider;
    }

    /// <summary>
    /// Gets a decoration provider for a document, checking in-process providers first,
    /// then falling back to language server providers.
    /// Returns null if no provider is mapped for this document type.
    /// </summary>
    /// <param name="document">The document to get a provider for.</param>
    /// <returns>A decoration provider, or null if no provider matches.</returns>
    public Documents.ITextDecorationProvider? GetDecorationProvider(Documents.IHex1bDocument document)
    {
        var filePath = document.FilePath;
        if (filePath == null) return null;

        var uri = PathToUri(filePath);

        // Check in-process providers first
        if (_inProcessDocumentProviders.TryGetValue(uri, out var inProcess))
            return inProcess;

        var fileName = Path.GetFileName(filePath);
        foreach (var (glob, providerId) in _inProcessMappings)
        {
            if (GlobMatch(fileName, glob) && _inProcessProviderFactories.TryGetValue(providerId, out var factory))
            {
                var provider = factory();
                _inProcessDocumentProviders[uri] = provider;
                return provider;
            }
        }

        // Fall back to language server providers
        return GetProvider(document);
    }

    private string? ResolveServer(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        foreach (var (glob, serverId) in _serverMappings)
        {
            if (GlobMatch(fileName, glob))
                return serverId;
        }

        return null;
    }

    private LanguageServerClient GetOrCreateClient(string serverId, LanguageServerConfiguration config)
    {
        if (_activeClients.TryGetValue(serverId, out var existing))
            return existing;

        var client = new LanguageServerClient(config);
        _activeClients[serverId] = client;

        _ = Task.Run(async () =>
        {
            try
            {
                await client.StartAsync().ConfigureAwait(false);

                if (client.ServerCapabilities?.SemanticTokensProvider != null)
                {
                    try
                    {
                        var provider = client.ServerCapabilities.SemanticTokensProvider.Value;
                        if (provider.TryGetProperty("legend", out var legend) &&
                            legend.TryGetProperty("tokenTypes", out var types))
                        {
                            _tokenLegends[serverId] = JsonSerializer.Deserialize<string[]>(types.GetRawText())
                                ?? SemanticTokenTypes.All;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Workspace: failed to start {serverId}: {ex.Message}");
            }
        });

        return client;
    }

    // ── Utilities ────────────────────────────────────────────

    private static string PathToUri(string path) =>
        "file://" + path.Replace('\\', '/');

    private static bool GlobMatch(string fileName, string pattern)
    {
        // Simple glob: supports * and {a,b} patterns
        if (pattern.StartsWith("*."))
        {
            var ext = pattern[1..]; // ".cs"
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.Contains('{'))
        {
            // Expand {a,b} and check each
            var braceStart = pattern.IndexOf('{');
            var braceEnd = pattern.IndexOf('}');
            if (braceStart >= 0 && braceEnd > braceStart)
            {
                var prefix = pattern[..braceStart];
                var suffix = pattern[(braceEnd + 1)..];
                var alternatives = pattern[(braceStart + 1)..braceEnd].Split(',');
                return alternatives.Any(alt => GlobMatch(fileName, prefix + alt + suffix));
            }
        }

        // Exact match fallback
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string InferLanguageId(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".tsx" => "typescriptreact",
            ".js" => "javascript",
            ".py" => "python",
            ".rs" => "rust",
            ".go" => "go",
            ".c" or ".h" => "c",
            ".cpp" or ".cc" or ".hpp" => "cpp",
            ".java" => "java",
            ".rb" => "ruby",
            ".lua" => "lua",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".md" => "markdown",
            ".sql" => "sql",
            _ => "plaintext",
        };
    }

    /// <summary>Disposes all server connections and clears document tracking.</summary>
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
        _openDocuments.Clear();
    }
}
