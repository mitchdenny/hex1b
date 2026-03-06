using Hex1b.LanguageServer.Protocol;

namespace Hex1b.LanguageServer;

/// <summary>
/// Configuration for connecting to a language server.
/// Built via the fluent API on <see cref="LanguageServerExtensions"/>.
/// </summary>
public sealed class LanguageServerConfiguration
{
    /// <summary>Path to the language server executable.</summary>
    public string? ServerCommand { get; set; }

    /// <summary>Arguments to pass to the server executable.</summary>
    public string[]? ServerArguments { get; set; }

    /// <summary>Unix domain socket path for connecting to an existing server.</summary>
    public string? SocketPath { get; set; }

    /// <summary>Working directory for the server process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>LSP language identifier (e.g., "csharp", "cpp", "python").</summary>
    public string? LanguageId { get; set; }

    /// <summary>Root URI for the workspace.</summary>
    public string? RootUri { get; set; }

    /// <summary>Document URI for the editor's document.</summary>
    public string? DocumentUri { get; set; }

    /// <summary>Whether to request semantic highlighting from the server.</summary>
    public bool EnableSemanticHighlightingValue { get; set; } = true;

    /// <summary>Whether to show diagnostics from the server.</summary>
    public bool EnableDiagnosticsValue { get; set; } = true;

    /// <summary>Whether to enable completion support.</summary>
    public bool EnableCompletionValue { get; set; } = true;

    /// <summary>Pre-configured transport for testing/in-process servers.</summary>
    internal JsonRpcTransport? Transport { get; set; }

    // ── Fluent API ──────────────────────────────────────────

    /// <summary>Sets the language server command to launch.</summary>
    public LanguageServerConfiguration WithServerCommand(string command, params string[] args)
    {
        ServerCommand = command;
        ServerArguments = args;
        return this;
    }

    /// <summary>Sets the working directory for the server process.</summary>
    public LanguageServerConfiguration WithWorkingDirectory(string path)
    {
        WorkingDirectory = path;
        return this;
    }

    /// <summary>Connects to an existing server via Unix domain socket.</summary>
    public LanguageServerConfiguration WithSocket(string socketPath)
    {
        SocketPath = socketPath;
        return this;
    }

    /// <summary>Sets the LSP language identifier.</summary>
    public LanguageServerConfiguration WithLanguageId(string languageId)
    {
        LanguageId = languageId;
        return this;
    }

    /// <summary>Sets the workspace root URI.</summary>
    public LanguageServerConfiguration WithRootUri(string rootUri)
    {
        RootUri = rootUri;
        return this;
    }

    /// <summary>Enables or disables semantic highlighting.</summary>
    public LanguageServerConfiguration EnableSemanticHighlighting(bool enable = true)
    {
        EnableSemanticHighlightingValue = enable;
        return this;
    }

    /// <summary>Enables or disables diagnostic underlines.</summary>
    public LanguageServerConfiguration EnableDiagnostics(bool enable = true)
    {
        EnableDiagnosticsValue = enable;
        return this;
    }

    /// <summary>Enables or disables completion support.</summary>
    public LanguageServerConfiguration EnableCompletion(bool enable = true)
    {
        EnableCompletionValue = enable;
        return this;
    }

    /// <summary>
    /// Sets a pre-configured transport (for in-process servers or testing).
    /// When set, no process is spawned and no socket is opened.
    /// </summary>
    /// <param name="serverOutputStream">Stream to read server responses from.</param>
    /// <param name="serverInputStream">Stream to write client requests to.</param>
    public LanguageServerConfiguration WithTransport(Stream serverOutputStream, Stream serverInputStream)
    {
        Transport = new Protocol.JsonRpcTransport(serverOutputStream, serverInputStream);
        return this;
    }
}
