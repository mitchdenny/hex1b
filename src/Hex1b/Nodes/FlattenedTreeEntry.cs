using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Internal structure for flattened tree view (used for rendering/navigation).
/// </summary>
internal readonly record struct FlattenedTreeEntry(
    TreeItemNode Node,
    int Depth,
    bool[] IsLastAtDepth);
