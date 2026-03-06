using System.Runtime.CompilerServices;
using Hex1b.LanguageServer;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for adding language server support to the <see cref="EditorWidget"/>.
/// </summary>
public static class LanguageServerExtensions
{
    // Cache providers so the same widget builder call reuses the same instance across renders.
    // Keyed by configuration callback delegate identity.
    private static readonly ConditionalWeakTable<object, LanguageServerDecorationProvider> s_providers = new();

    /// <summary>
    /// Connects a language server to this editor, enabling semantic highlighting,
    /// diagnostic underlines, and completion overlays.
    /// </summary>
    /// <remarks>
    /// The provider is cached per delegate instance so repeated widget rebuilds
    /// reuse the same connection. For explicit control, create a
    /// <see cref="LanguageServerDecorationProvider"/> and use <c>.Decorations(provider)</c>.
    /// </remarks>
    /// <param name="editor">The editor widget to enhance.</param>
    /// <param name="configure">Configuration callback for the language server connection.</param>
    /// <returns>A new editor widget with the language server decoration provider added.</returns>
    public static EditorWidget LanguageServer(this EditorWidget editor, Action<LanguageServerConfiguration> configure)
    {
        var provider = s_providers.GetValue(configure, key =>
        {
            var config = new LanguageServerConfiguration();
            ((Action<LanguageServerConfiguration>)key)(config);
            return new LanguageServerDecorationProvider(config);
        });

        return editor.Decorations(provider);
    }

    /// <summary>
    /// Connects this editor to a language server managed by a workspace.
    /// The workspace handles server lifecycle and connection sharing — multiple
    /// documents of the same language share a single server process.
    /// </summary>
    /// <param name="editor">The editor widget to enhance.</param>
    /// <param name="workspace">The workspace that manages server instances.</param>
    /// <param name="documentUri">
    /// The document's URI (e.g., <c>file:///path/to/file.cs</c>).
    /// Used to identify this document to the language server.
    /// </param>
    /// <param name="languageId">
    /// LSP language identifier. If null, inferred from the document URI extension.
    /// </param>
    /// <returns>A new editor widget with the workspace-managed decoration provider.</returns>
    /// <example>
    /// <code>
    /// var workspace = new Hex1bLanguageServerWorkspace("/my/project");
    /// workspace.RegisterServer("csharp", lsp => lsp.WithServerCommand("csharp-ls"));
    ///
    /// ctx.Editor(state).LanguageServer(workspace, "file:///my/project/Program.cs")
    /// </code>
    /// </example>
    public static EditorWidget LanguageServer(
        this EditorWidget editor,
        Hex1bLanguageServerWorkspace workspace,
        string documentUri,
        string? languageId = null)
    {
        var provider = workspace.GetProvider(documentUri, languageId);
        return editor.Decorations(provider);
    }

    /// <summary>
    /// Connects this editor to a language server managed by a <see cref="Hex1bDocumentWorkspace"/>.
    /// The workspace resolves the appropriate server based on the document's file path
    /// and glob mappings registered with <see cref="Hex1bDocumentWorkspace.MapLanguageServer"/>.
    /// </summary>
    /// <param name="editor">The editor widget to enhance.</param>
    /// <param name="workspace">The document workspace that manages servers.</param>
    /// <returns>The editor widget, with a decoration provider if a server matches.</returns>
    public static EditorWidget LanguageServer(
        this EditorWidget editor,
        Hex1bDocumentWorkspace workspace)
    {
        var provider = workspace.GetProvider(editor.State.Document);
        return provider != null ? editor.Decorations(provider) : editor;
    }
}
