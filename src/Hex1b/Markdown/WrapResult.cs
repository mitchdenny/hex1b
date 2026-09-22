using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// Result of wrapping styled words into lines. Contains the rendered ANSI lines
/// along with link position metadata.
/// </summary>
internal readonly record struct WrapResult(
    IReadOnlyList<string> Lines,
    IReadOnlyList<LinkRegionInfo> LinkRegions);
