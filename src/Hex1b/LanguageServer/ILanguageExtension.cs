using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Widgets;

namespace Hex1b.LanguageServer;

/// <summary>
/// Defines per-language customization for LSP feature behavior.
/// Implementations can enable/disable individual features, customize rendering,
/// and filter/transform LSP results before they are displayed.
/// </summary>
internal interface ILanguageExtension
{
    /// <summary>
    /// The LSP language identifier (e.g., "csharp", "typescript", "python").
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// The set of LSP features this extension enables.
    /// Features not in this set will not be requested from the server.
    /// </summary>
    LspFeatureSet EnabledFeatures { get; }

    /// <summary>
    /// Customizes hover overlay rendering. Return null to use the default renderer.
    /// </summary>
    EditorOverlay? RenderHoverOverlay(HoverResult hover, DocumentPosition position) => null;

    /// <summary>
    /// Filters or transforms completion items before display. Return the original
    /// list unmodified to use all items.
    /// </summary>
    IReadOnlyList<CompletionItem> FilterCompletions(IReadOnlyList<CompletionItem> items) => items;

    /// <summary>
    /// Filters or transforms code actions before display. Return the original
    /// list unmodified to use all actions.
    /// </summary>
    IReadOnlyList<CodeAction> FilterCodeActions(IReadOnlyList<CodeAction> actions) => actions;

    /// <summary>
    /// Provides language-specific server configuration overrides.
    /// Return null to use defaults.
    /// </summary>
    object? GetServerConfiguration() => null;
}
