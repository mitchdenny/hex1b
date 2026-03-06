using Hex1b.LanguageServer;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for adding language server support to the <see cref="EditorWidget"/>.
/// </summary>
public static class LanguageServerExtensions
{
    /// <summary>
    /// Connects a language server to this editor, enabling semantic highlighting,
    /// diagnostic underlines, and completion overlays.
    /// </summary>
    /// <param name="editor">The editor widget to enhance.</param>
    /// <param name="configure">Configuration callback for the language server connection.</param>
    /// <returns>A new editor widget with the language server decoration provider added.</returns>
    /// <example>
    /// <code>
    /// ctx.Editor(state).LanguageServer(lsp => lsp
    ///     .WithServerCommand("clangd", "--log=error")
    ///     .WithLanguageId("cpp")
    ///     .WithRootUri("/path/to/project"));
    /// </code>
    /// </example>
    public static EditorWidget LanguageServer(this EditorWidget editor, Action<LanguageServerConfiguration> configure)
    {
        var config = new LanguageServerConfiguration();
        configure(config);

        var provider = new LanguageServerDecorationProvider(config);
        return editor.Decorations(provider);
    }
}
