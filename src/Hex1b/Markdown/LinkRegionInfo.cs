using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// Describes the position and metadata of a link region within wrapped text.
/// Used by <see cref="Hex1b.Nodes.MarkdownTextBlockNode"/> to create and
/// position <see cref="Hex1b.Nodes.MarkdownLinkRegionNode"/> children.
/// </summary>
internal readonly record struct LinkRegionInfo(
    int LinkId,
    string Url,
    string Text,
    int LineIndex,
    int ColumnOffset,
    int DisplayWidth);
