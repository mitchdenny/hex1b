namespace Hex1b.Widgets;

/// <summary>
/// A vertical splitter/divider that separates left and right panes.
/// </summary>
public sealed record SplitterWidget(Hex1bWidget Left, Hex1bWidget Right, int LeftWidth = 30) : Hex1bWidget;
