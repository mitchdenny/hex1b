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
}
