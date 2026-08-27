namespace Hex1b.Reflow;

internal static class InternalTerminalReflow
{
    internal static bool TryReflow(
        ITerminalReflowProvider provider,
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors,
        out InternalReflowResult result)
    {
        if (provider is AutoReflowStrategy auto)
        {
            return TryReflow(
                auto.DetectedStrategy,
                context,
                anchors,
                out result);
        }

        if (context.InAlternateScreen ||
            provider is NoReflowStrategy or XtermReflowStrategy or ITerm2ReflowStrategy)
        {
            result = PerformNoReflow(provider, context, anchors);
            return true;
        }

        switch (provider)
        {
            case KittyReflowStrategy or WezTermReflowStrategy:
                result = ReflowHelper.PerformReflowWithAnchors(
                    context,
                    preserveCursorRow: true,
                    reflowSavedCursor: false,
                    anchors);
                return true;
            case GhosttyReflowStrategy or FootReflowStrategy or VteReflowStrategy:
                result = ReflowHelper.PerformReflowWithAnchors(
                    context,
                    preserveCursorRow: true,
                    reflowSavedCursor: true,
                    anchors);
                return true;
            case AlacrittyReflowStrategy or WindowsTerminalReflowStrategy:
                result = ReflowHelper.PerformReflowWithAnchors(
                    context,
                    preserveCursorRow: false,
                    reflowSavedCursor: false,
                    anchors);
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static InternalReflowResult PerformNoReflow(
        ITerminalReflowProvider provider,
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors)
    {
        var reflow = provider.Reflow(context);
        var oldHistoryCount = context.ScrollbackRows.Length;
        var newHistoryCount = reflow.ScrollbackRows.Length;
        var mapped = new List<TerminalReflowAnchor>(anchors.Count);
        foreach (var anchor in anchors)
        {
            if (anchor.Row < oldHistoryCount)
            {
                if (anchor.Row < newHistoryCount)
                {
                    mapped.Add(anchor with
                    {
                        Column = Math.Clamp(anchor.Column, 0, context.NewWidth - 1)
                    });
                }

                continue;
            }

            var screenRow = anchor.Row - oldHistoryCount;
            if (screenRow >= 0 && screenRow < context.NewHeight)
            {
                mapped.Add(anchor with
                {
                    Row = checked(newHistoryCount + screenRow),
                    Column = Math.Clamp(anchor.Column, 0, context.NewWidth - 1)
                });
            }
        }

        return new InternalReflowResult(reflow, mapped);
    }
}
