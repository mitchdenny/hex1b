namespace Hex1b.Reflow;

internal readonly record struct InternalReflowResult(
    ReflowResult Reflow,
    IReadOnlyList<TerminalReflowAnchor> Anchors);
