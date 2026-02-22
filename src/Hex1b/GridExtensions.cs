using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="GridWidget"/> instances using the fluent builder API.
/// </summary>
public static class GridExtensions
{
    /// <summary>
    /// Creates a grid layout where cells are positioned explicitly using row/column indices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the <see cref="GridContext"/> parameter to define cells, and optionally configure
    /// explicit column and row definitions via <see cref="GridContext.Columns"/> and
    /// <see cref="GridContext.Rows"/>. Columns and rows not explicitly defined are auto-created
    /// from cell positions with <see cref="Hex1b.Layout.SizeHint.Content"/> sizing.
    /// </para>
    /// <example>
    /// <code>
    /// ctx.Grid(g => [
    ///     g.Cell(c => c.Text("Nav")).RowSpan(0, 2).Column(0).Width(20),
    ///     g.Cell(c => c.Text("Header")).Row(0).Column(1),
    ///     g.Cell(c => c.Text("Content")).Row(1).Column(1).FillHeight(),
    /// ])
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="ctx">The parent widget context.</param>
    /// <param name="builder">A callback that receives a <see cref="GridContext"/> and returns an array of <see cref="GridCellWidget"/>.</param>
    /// <returns>A configured <see cref="GridWidget"/>.</returns>
    public static GridWidget Grid<TParent>(
        this WidgetContext<TParent> ctx,
        Func<GridContext, GridCellWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var gridCtx = new GridContext();
        var cells = builder(gridCtx);
        return new GridWidget(cells, gridCtx.Columns.ToArray(), gridCtx.Rows.ToArray());
    }
}
