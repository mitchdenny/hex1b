using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating and configuring TableWidget.
/// </summary>
public static class TableExtensions
{
    /// <summary>
    /// Creates a new table widget with the specified data.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The data source for table rows. Pass null to show loading state.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow>(this RootContext ctx, IReadOnlyList<TRow>? data)
        => new() { Data = data };

    /// <summary>
    /// Creates a new table widget with the specified data.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The data source for table rows. Pass null to show loading state.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow, TParent>(
        this WidgetContext<TParent> ctx, 
        IReadOnlyList<TRow>? data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Configures the header cells for the table.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="builder">A function that returns the header cells.</param>
    /// <returns>The table widget with header configured.</returns>
    public static TableWidget<TRow> WithHeader<TRow>(
        this TableWidget<TRow> table,
        Func<TableHeaderContext, IReadOnlyList<TableCell>> builder)
        => table with { HeaderBuilder = builder };

    /// <summary>
    /// Configures the row cell builder for the table.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="builder">A function that builds cells for each row. Receives row context, row data, and row state.</param>
    /// <returns>The table widget with row builder configured.</returns>
    public static TableWidget<TRow> WithRow<TRow>(
        this TableWidget<TRow> table,
        Func<TableRowContext, TRow, TableRowState, IReadOnlyList<TableCell>> builder)
        => table with { RowBuilder = builder };

    /// <summary>
    /// Configures the footer cells for the table.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="builder">A function that returns the footer cells.</param>
    /// <returns>The table widget with footer configured.</returns>
    public static TableWidget<TRow> WithFooter<TRow>(
        this TableWidget<TRow> table,
        Func<TableFooterContext, IReadOnlyList<TableCell>> builder)
        => table with { FooterBuilder = builder };

    /// <summary>
    /// Configures the empty state widget shown when data is empty.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="builder">A function that builds the empty state widget.</param>
    /// <returns>The table widget with empty state configured.</returns>
    public static TableWidget<TRow> WithEmpty<TRow>(
        this TableWidget<TRow> table,
        Func<RootContext, Hex1bWidget> builder)
        => table with { EmptyBuilder = builder };

    /// <summary>
    /// Configures the loading state rows shown when data is null.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="builder">A function that builds loading placeholder cells.</param>
    /// <param name="rowCount">Number of loading rows to display.</param>
    /// <returns>The table widget with loading state configured.</returns>
    public static TableWidget<TRow> WithLoading<TRow>(
        this TableWidget<TRow> table,
        Func<TableLoadingContext, int, IReadOnlyList<TableCell>> builder,
        int rowCount = 3)
        => table with { LoadingRowBuilder = builder, LoadingRowCount = rowCount };

    /// <summary>
    /// Configures the row key selector for stable row identification across data changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="keySelector">A function that returns a unique key for each row.</param>
    /// <returns>The table widget with row key selector configured.</returns>
    public static TableWidget<TRow> WithRowKey<TRow>(
        this TableWidget<TRow> table,
        Func<TRow, object> keySelector)
        => table with { RowKeySelector = keySelector };

    /// <summary>
    /// Configures the focused row by key.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="focusedKey">The key of the row that has keyboard focus, or null for none.</param>
    /// <returns>The table widget with focus configured.</returns>
    public static TableWidget<TRow> WithFocus<TRow>(
        this TableWidget<TRow> table,
        object? focusedKey)
        => table with { FocusedKey = focusedKey };

    /// <summary>
    /// Configures the selected rows by keys.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="selectedKeys">The set of keys for selected rows.</param>
    /// <returns>The table widget with selection configured.</returns>
    public static TableWidget<TRow> WithSelection<TRow>(
        this TableWidget<TRow> table,
        IReadOnlySet<object>? selectedKeys)
        => table with { SelectedKeys = selectedKeys };

    /// <summary>
    /// Sets the handler for focus changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The handler to call when focus changes.</param>
    /// <returns>The table widget with focus handler configured.</returns>
    public static TableWidget<TRow> OnFocusChanged<TRow>(
        this TableWidget<TRow> table,
        Action<object?> handler)
        => table with { FocusChangedHandler = key => { handler(key); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for focus changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The async handler to call when focus changes.</param>
    /// <returns>The table widget with focus handler configured.</returns>
    public static TableWidget<TRow> OnFocusChanged<TRow>(
        this TableWidget<TRow> table,
        Func<object?, Task> handler)
        => table with { FocusChangedHandler = handler };

    /// <summary>
    /// Sets the handler for selection changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The handler to call when selection changes.</param>
    /// <returns>The table widget with selection handler configured.</returns>
    public static TableWidget<TRow> OnSelectionChanged<TRow>(
        this TableWidget<TRow> table,
        Action<IReadOnlySet<object>> handler)
        => table with { SelectionChangedHandler = keys => { handler(keys); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for selection changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The async handler to call when selection changes.</param>
    /// <returns>The table widget with selection handler configured.</returns>
    public static TableWidget<TRow> OnSelectionChanged<TRow>(
        this TableWidget<TRow> table,
        Func<IReadOnlySet<object>, Task> handler)
        => table with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets the handler for row activation (Enter key or double-click).
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The handler to call when a row is activated.</param>
    /// <returns>The table widget with activation handler configured.</returns>
    public static TableWidget<TRow> OnRowActivated<TRow>(
        this TableWidget<TRow> table,
        Action<object, TRow> handler)
        => table with { RowActivatedHandler = (key, row) => { handler(key, row); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for row activation.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The async handler to call when a row is activated.</param>
    /// <returns>The table widget with activation handler configured.</returns>
    public static TableWidget<TRow> OnRowActivated<TRow>(
        this TableWidget<TRow> table,
        Func<object, TRow, Task> handler)
        => table with { RowActivatedHandler = handler };

    /// <summary>
    /// Enables a selection column with checkboxes for multi-select.
    /// The selection column appears as the first column and allows clicking to select rows.
    /// Checkbox appearance is controlled by TableTheme.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <returns>The table widget with selection column enabled.</returns>
    public static TableWidget<TRow> WithSelectionColumn<TRow>(this TableWidget<TRow> table)
        => table with { ShowSelectionColumn = true };

    /// <summary>
    /// Sets the table to Compact render mode (no separators between rows).
    /// This is the default mode.
    /// </summary>
    /// <typeparam name="TRow">The row data type.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <returns>The table widget with Compact render mode.</returns>
    public static TableWidget<TRow> Compact<TRow>(this TableWidget<TRow> table)
        => table with { RenderMode = Widgets.TableRenderMode.Compact };

    /// <summary>
    /// Sets the table to Full render mode (horizontal separators between each row).
    /// </summary>
    /// <typeparam name="TRow">The row data type.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <returns>The table widget with Full render mode.</returns>
    public static TableWidget<TRow> Full<TRow>(this TableWidget<TRow> table)
        => table with { RenderMode = Widgets.TableRenderMode.Full };
}
