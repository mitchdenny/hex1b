using Hex1b.Data;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating TableWidget instances.
/// </summary>
public static class TableExtensions
{
    /// <summary>
    /// Creates a new table widget with the specified data.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="context">The root context.</param>
    /// <param name="data">The data source for table rows. Pass null to show loading state.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow>(this RootContext context, IReadOnlyList<TRow>? data)
        => new() { Data = data };

    /// <summary>
    /// Creates a new table widget with the specified data.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="data">The data source for table rows. Pass null to show loading state.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow, TParent>(
        this WidgetContext<TParent> context, 
        IReadOnlyList<TRow>? data)
        where TParent : Hex1bWidget
        => new() { Data = data };
    
    /// <summary>
    /// Creates a new table widget with an async data source for virtualized tables.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <param name="context">The root context.</param>
    /// <param name="dataSource">The async data source for virtualized data loading.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow>(this RootContext context, ITableDataSource<TRow> dataSource)
        => new() { DataSource = dataSource };
    
    /// <summary>
    /// Creates a new table widget with an async data source for virtualized tables.
    /// </summary>
    /// <typeparam name="TRow">The type of data for each row.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="dataSource">The async data source for virtualized data loading.</param>
    /// <returns>A new TableWidget instance.</returns>
    public static TableWidget<TRow> Table<TRow, TParent>(
        this WidgetContext<TParent> context, 
        ITableDataSource<TRow> dataSource)
        where TParent : Hex1bWidget
        => new() { DataSource = dataSource };
}
