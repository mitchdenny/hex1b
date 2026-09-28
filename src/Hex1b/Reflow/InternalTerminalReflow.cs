namespace Hex1b.Reflow;

internal static class InternalTerminalReflow
{
    internal static bool TryReflow(
        ITerminalReflowProvider provider,
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors,
        out InternalReflowResult result)
    {
        if (provider is IInternalTerminalReflowProvider internalProvider)
            return internalProvider.TryReflowWithAnchors(context, anchors, out result);

        if (provider is AutoReflowStrategy auto)
        {
            return TryReflow(
                auto.DetectedStrategy,
                context,
                anchors,
                out result);
        }

        if ((context.InAlternateScreen &&
             provider is KittyReflowStrategy or WezTermReflowStrategy or GhosttyReflowStrategy or
                 FootReflowStrategy or VteReflowStrategy or AlacrittyReflowStrategy or WindowsTerminalReflowStrategy) ||
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
        reflow = reflow with { LineRenditions = context.ResizeLineRenditions() };
        var oldHistoryCount = context.ScrollbackRows.Length;
        var newHistoryCount = reflow.ScrollbackRows.Length;
        var mapped = new List<TerminalReflowAnchor>(anchors.Count);
        foreach (var anchor in anchors)
        {
            if (anchor.Row < oldHistoryCount)
            {
                if (anchor.Row < newHistoryCount &&
                    (!anchor.IsTextPosition || anchor.Column <= reflow.ScrollbackRows[anchor.Row].Cells.Length))
                    mapped.Add(anchor);

                continue;
            }

            var screenRow = anchor.Row - oldHistoryCount;
            if (screenRow >= 0 && screenRow < context.NewHeight &&
                (!anchor.IsTextPosition || RetainsTextColumn(context.ScreenRows[screenRow].Length, context.NewWidth,
                    screenRow < context.LineRenditions.Length ? context.LineRenditions[screenRow] : LineRendition.SingleWidth,
                    anchor.Column, anchor.Column < context.ScreenRows[screenRow].Length
                        ? context.ScreenRows[screenRow][anchor.Column].Character : null)))
            {
                mapped.Add(anchor with
                {
                    Row = checked(newHistoryCount + screenRow)
                });
            }
        }

        return new InternalReflowResult(reflow, mapped);
    }

    internal static bool RetainsTextColumn(int oldWidth, int newWidth, LineRendition rendition, int column, string? character)
    {
        var oldCapacity = rendition == LineRendition.SingleWidth ? oldWidth : Math.Max(1, oldWidth / 2);
        var newCapacity = rendition == LineRendition.SingleWidth ? newWidth : Math.Max(1, newWidth / 2);
        if (column == oldCapacity)
            return column <= newCapacity;
        return column >= 0 && column < Math.Min(oldCapacity, newCapacity) &&
            DisplayWidth.GetGraphemeWidth(character ?? "") <= newCapacity - column;
    }
}
