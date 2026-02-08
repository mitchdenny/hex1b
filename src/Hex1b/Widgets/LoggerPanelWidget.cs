using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Logging;
using Hex1b.Nodes;
using Hex1b.Theming;
using Microsoft.Extensions.Logging;

namespace Hex1b.Widgets;

/// <summary>
/// A composite widget that displays log entries from an <see cref="IHex1bLogStore"/> in a table
/// with automatic follow behavior. Follows by default; scrolling up breaks the lock.
/// Navigating back to the last row re-engages following.
/// </summary>
/// <param name="LogStore">The opaque log store handle returned by <c>AddHex1b()</c>.</param>
public sealed record LoggerPanelWidget(IHex1bLogStore LogStore) : CompositeWidget<LoggerPanelNode>
{
    /// <inheritdoc />
    protected override void UpdateNode(LoggerPanelNode node)
    {
        node.LogStore = LogStore;
    }

    /// <inheritdoc />
    protected override Task<Hex1bWidget> BuildContentAsync(LoggerPanelNode node, ReconcileContext context)
    {
        var store = (Hex1bLogStore)LogStore;
        var dataSource = store.DataSource;
        var count = store.Buffer.Count;

        // Check the table node for user-initiated scroll/navigation.
        // If the user scrolled away, break the follow lock.
        // If they navigated back to the end, re-engage it.
        var tableNode = LoggerPanelNode.FindTableNodePublic(node.ContentChild);
        if (tableNode != null)
        {
            if (tableNode.UserScrolledAway)
                node.IsFollowing = false;
            else if (!node.IsFollowing && !tableNode.UserScrolledAway)
                node.IsFollowing = true;
        }

        // When following, use the last item's index as the focus key.
        object? focusedKey = node.IsFollowing && count > 0
            ? (object)(count - 1)
            : tableNode?.FocusedKey;

        var table = new TableWidget<Hex1bLogEntry>() { DataSource = dataSource }
            .RowKey(e => e.Timestamp.Ticks ^ e.Message.GetHashCode())
            .Header(h => [
                h.Cell("Time").Width(SizeHint.Fixed(12)),
                h.Cell("Level").Width(SizeHint.Fixed(7)),
                h.Cell("Category").Width(SizeHint.Fixed(30)),
                h.Cell("Message").Width(SizeHint.Fill)
            ])
            .Row((r, entry, state) => [
                r.Cell(entry.Timestamp.ToString("HH:mm:ss.fff")),
                ColoredCell(r, FormatLevel(entry.Level), entry.Level),
                r.Cell(ShortenCategory(entry.Category)),
                r.Cell(entry.Message)
            ])
            .Focus(focusedKey)
            .FillWidth()
            .FillHeight();

        // When following, scroll the table to the end after reconciliation
        if (node.IsFollowing)
        {
            node.ScrollTableToEnd();
        }

        return Task.FromResult<Hex1bWidget>(table);
    }

    private static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "????"
    };

    private static string ShortenCategory(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 ? category[(lastDot + 1)..] : category;
    }

    private static TableCell ColoredCell(TableRowContext r, string text, LogLevel level)
    {
        return r.Cell(ctx =>
            new ThemePanelWidget(
                theme => theme.Set(GlobalTheme.ForegroundColor, GetLevelColor(level)),
                new TextBlockWidget(text)));
    }

    private static Hex1bColor GetLevelColor(LogLevel level) => level switch
    {
        LogLevel.Trace => LoggerPanelTheme.TraceColor.DefaultValue(),
        LogLevel.Debug => LoggerPanelTheme.DebugColor.DefaultValue(),
        LogLevel.Information => LoggerPanelTheme.InformationColor.DefaultValue(),
        LogLevel.Warning => LoggerPanelTheme.WarningColor.DefaultValue(),
        LogLevel.Error => LoggerPanelTheme.ErrorColor.DefaultValue(),
        LogLevel.Critical => LoggerPanelTheme.CriticalColor.DefaultValue(),
        _ => Hex1bColor.Default
    };
}
