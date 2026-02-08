using Hex1b.Logging;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating LoggerPanelWidget.
/// </summary>
public static class LoggerPanelExtensions
{
    /// <summary>
    /// Creates a logger panel that displays log entries from the specified log store.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="logStore">The log store handle returned by <c>builder.Logging.AddHex1b(out var logStore)</c>.</param>
    public static LoggerPanelWidget LoggerPanel<TParent>(
        this WidgetContext<TParent> ctx,
        IHex1bLogStore logStore)
        where TParent : Hex1bWidget
        => new(logStore);
}
