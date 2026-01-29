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
    /// <param name="builder">A function that builds cells for each row.</param>
    /// <returns>The table widget with row builder configured.</returns>
    public static TableWidget<TRow> WithRow<TRow>(
        this TableWidget<TRow> table,
        Func<TableRowContext, TRow, IReadOnlyList<TableCell>> builder)
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
    /// Configures row selection for the table.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="selectedIndex">The currently selected row index.</param>
    /// <returns>The table widget with selection configured.</returns>
    public static TableWidget<TRow> WithSelection<TRow>(
        this TableWidget<TRow> table,
        int? selectedIndex)
        => table with { SelectedIndex = selectedIndex };

    /// <summary>
    /// Sets the handler for selection changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The handler to call when selection changes.</param>
    /// <returns>The table widget with selection handler configured.</returns>
    public static TableWidget<TRow> OnSelectionChanged<TRow>(
        this TableWidget<TRow> table,
        Action<int> handler)
        => table with { SelectionChangedHandler = idx => { handler(idx); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for selection changes.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The async handler to call when selection changes.</param>
    /// <returns>The table widget with selection handler configured.</returns>
    public static TableWidget<TRow> OnSelectionChanged<TRow>(
        this TableWidget<TRow> table,
        Func<int, Task> handler)
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
        Action<int, TRow> handler)
        => table with { RowActivatedHandler = (idx, row) => { handler(idx, row); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for row activation.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="table">The table widget.</param>
    /// <param name="handler">The async handler to call when a row is activated.</param>
    /// <returns>The table widget with activation handler configured.</returns>
    public static TableWidget<TRow> OnRowActivated<TRow>(
        this TableWidget<TRow> table,
        Func<int, TRow, Task> handler)
        => table with { RowActivatedHandler = handler };
}
