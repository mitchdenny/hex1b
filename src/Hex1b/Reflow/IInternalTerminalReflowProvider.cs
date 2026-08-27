namespace Hex1b.Reflow;

internal interface IInternalTerminalReflowProvider
{
    bool TryReflowWithAnchors(
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors,
        out InternalReflowResult result);
}
