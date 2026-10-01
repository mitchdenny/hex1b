using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Builder context for constructing a <see cref="GridWidget"/>.
/// Provides <see cref="Columns"/> and <see cref="Rows"/> collections for explicit
/// grid dimension definitions, and a <see cref="Cell"/> method for creating cells.
/// </summary>
public sealed class GridContext : WidgetContext<GridWidget>
{
    /// <summary>
    /// The explicit column definitions for the grid.
    /// Columns not covered by explicit definitions are auto-created from cell positions.
    /// </summary>
    public GridDefinitionCollection<GridColumnDefinition> Columns { get; } = new();

    /// <summary>
    /// The explicit row definitions for the grid.
    /// Rows not covered by explicit definitions are auto-created from cell positions.
    /// </summary>
    public GridDefinitionCollection<GridRowDefinition> Rows { get; } = new();

    /// <summary>
    /// Creates a grid cell containing the widget returned by the builder callback.
    /// </summary>
    /// <param name="builder">A callback that builds the cell's content widget.</param>
    /// <returns>A <see cref="GridCellWidget"/> that can be further configured with row/column placement.</returns>
    public GridCellWidget Cell(Func<WidgetContext<GridWidget>, Hex1bWidget> builder)
    {
        var childCtx = new WidgetContext<GridWidget>();
        var child = builder(childCtx);
        return new GridCellWidget(child);
    }

    internal GridContext() { }
}
