namespace Hex1b.Widgets;

public sealed record ButtonWidget(string Label, Action OnClick) : Hex1bWidget;
