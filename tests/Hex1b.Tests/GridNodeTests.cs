using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class GridNodeTests
{
    #region Measure Tests

    [Fact]
    public void Measure_EmptyGrid_ReturnsZero()
    {
        var node = CreateGridNode([], 0, 0, [], []);
        var size = node.Measure(Constraints.Unbounded);
        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_SingleCell_ReturnsContentSize()
    {
        var child = new TextBlockNode { Text = "Hello" }; // 5 wide, 1 tall
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, Row: 0, Column: 0, RowSpan: 1, ColumnSpan: 1)],
            columnCount: 1, rowCount: 1,
            colHints: [SizeHint.Content],
            rowHints: [SizeHint.Content]);

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_TwoColumnContent_SumsWidths()
    {
        var left = new TextBlockNode { Text = "Left" };   // 4 wide
        var right = new TextBlockNode { Text = "Right!" }; // 6 wide
        var node = CreateGridNode(
            [
                new GridNode.CellEntry(left, 0, 0, 1, 1),
                new GridNode.CellEntry(right, 0, 1, 1, 1),
            ],
            columnCount: 2, rowCount: 1,
            colHints: [SizeHint.Content, SizeHint.Content],
            rowHints: [SizeHint.Content]);

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(10, size.Width); // 4 + 6
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_TwoRowContent_SumsHeights()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = CreateGridNode(
            [
                new GridNode.CellEntry(top, 0, 0, 1, 1),
                new GridNode.CellEntry(bottom, 1, 0, 1, 1),
            ],
            columnCount: 1, rowCount: 2,
            colHints: [SizeHint.Content],
            rowHints: [SizeHint.Content, SizeHint.Content]);

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(6, size.Width); // max("Top", "Bottom")
        Assert.Equal(2, size.Height); // 1 + 1
    }

    [Fact]
    public void Measure_FixedColumns_UsesFixedWidths()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, 0, 0, 1, 1)],
            columnCount: 2, rowCount: 1,
            colHints: [SizeHint.Fixed(20), SizeHint.Fixed(30)],
            rowHints: [SizeHint.Content]);

        var size = node.Measure(new Constraints(0, 100, 0, 100));

        Assert.Equal(50, size.Width); // 20 + 30
    }

    [Fact]
    public void Measure_FillColumns_DistributeRemainingSpace()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, 0, 0, 1, 1)],
            columnCount: 3, rowCount: 1,
            colHints: [SizeHint.Fixed(10), SizeHint.Fill, SizeHint.Fill],
            rowHints: [SizeHint.Content]);

        var size = node.Measure(new Constraints(0, 100, 0, 100));

        Assert.Equal(100, size.Width); // 10 + 45 + 45
    }

    [Fact]
    public void Measure_WeightedFillColumns_DistributeProportionally()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, 0, 0, 1, 1)],
            columnCount: 2, rowCount: 1,
            colHints: [SizeHint.Weighted(1), SizeHint.Weighted(3)],
            rowHints: [SizeHint.Content]);

        var size = node.Measure(new Constraints(0, 80, 0, 100));

        Assert.Equal(80, size.Width); // 20 + 60
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_SingleCell_FillsBounds()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, 0, 0, 1, 1)],
            columnCount: 1, rowCount: 1,
            colHints: [SizeHint.Content],
            rowHints: [SizeHint.Content]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, child.Bounds.X);
        Assert.Equal(0, child.Bounds.Y);
    }

    [Fact]
    public void Arrange_TwoFixedColumns_PositionsCorrectly()
    {
        var left = new TextBlockNode { Text = "L" };
        var right = new TextBlockNode { Text = "R" };
        var node = CreateGridNode(
            [
                new GridNode.CellEntry(left, 0, 0, 1, 1),
                new GridNode.CellEntry(right, 0, 1, 1, 1),
            ],
            columnCount: 2, rowCount: 1,
            colHints: [SizeHint.Fixed(20), SizeHint.Fixed(30)],
            rowHints: [SizeHint.Content]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, left.Bounds.X);
        Assert.Equal(20, left.Bounds.Width);
        Assert.Equal(20, right.Bounds.X);
        Assert.Equal(30, right.Bounds.Width);
    }

    [Fact]
    public void Arrange_TwoFixedRows_PositionsCorrectly()
    {
        var top = new TextBlockNode { Text = "T" };
        var bottom = new TextBlockNode { Text = "B" };
        var node = CreateGridNode(
            [
                new GridNode.CellEntry(top, 0, 0, 1, 1),
                new GridNode.CellEntry(bottom, 1, 0, 1, 1),
            ],
            columnCount: 1, rowCount: 2,
            colHints: [SizeHint.Content],
            rowHints: [SizeHint.Fixed(5), SizeHint.Fixed(10)]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, top.Bounds.Y);
        Assert.Equal(5, top.Bounds.Height);
        Assert.Equal(5, bottom.Bounds.Y);
        Assert.Equal(10, bottom.Bounds.Height);
    }

    [Fact]
    public void Arrange_MixedFixedAndFillColumns_DistributesCorrectly()
    {
        var nav = new TextBlockNode { Text = "Nav" };
        var content = new TextBlockNode { Text = "Content" };
        var node = CreateGridNode(
            [
                new GridNode.CellEntry(nav, 0, 0, 1, 1),
                new GridNode.CellEntry(content, 0, 1, 1, 1),
            ],
            columnCount: 2, rowCount: 1,
            colHints: [SizeHint.Fixed(20), SizeHint.Fill],
            rowHints: [SizeHint.Content]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, nav.Bounds.X);
        Assert.Equal(20, nav.Bounds.Width);
        Assert.Equal(20, content.Bounds.X);
        Assert.Equal(60, content.Bounds.Width); // 80 - 20
    }

    [Fact]
    public void Arrange_WithOffset_PositionsRelativeToOrigin()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = CreateGridNode(
            [new GridNode.CellEntry(child, 0, 0, 1, 1)],
            columnCount: 1, rowCount: 1,
            colHints: [SizeHint.Fixed(20)],
            rowHints: [SizeHint.Fixed(5)]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(10, 5, 80, 24));

        Assert.Equal(10, child.Bounds.X);
        Assert.Equal(5, child.Bounds.Y);
    }

    #endregion

    #region Spanning Tests

    [Fact]
    public void Arrange_RowSpan_CellSpansMultipleRows()
    {
        var nav = new TextBlockNode { Text = "Nav" };
        var header = new TextBlockNode { Text = "Header" };
        var content = new TextBlockNode { Text = "Content" };

        var node = CreateGridNode(
            [
                new GridNode.CellEntry(nav, Row: 0, Column: 0, RowSpan: 2, ColumnSpan: 1),
                new GridNode.CellEntry(header, Row: 0, Column: 1, RowSpan: 1, ColumnSpan: 1),
                new GridNode.CellEntry(content, Row: 1, Column: 1, RowSpan: 1, ColumnSpan: 1),
            ],
            columnCount: 2, rowCount: 2,
            colHints: [SizeHint.Fixed(20), SizeHint.Fill],
            rowHints: [SizeHint.Fixed(3), SizeHint.Fixed(7)]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Nav spans rows 0-1
        Assert.Equal(0, nav.Bounds.X);
        Assert.Equal(0, nav.Bounds.Y);
        Assert.Equal(20, nav.Bounds.Width);
        Assert.Equal(10, nav.Bounds.Height); // 3 + 7

        // Header is row 0, col 1
        Assert.Equal(20, header.Bounds.X);
        Assert.Equal(0, header.Bounds.Y);
        Assert.Equal(60, header.Bounds.Width);
        Assert.Equal(3, header.Bounds.Height);

        // Content is row 1, col 1
        Assert.Equal(20, content.Bounds.X);
        Assert.Equal(3, content.Bounds.Y);
        Assert.Equal(60, content.Bounds.Width);
        Assert.Equal(7, content.Bounds.Height);
    }

    [Fact]
    public void Arrange_ColumnSpan_CellSpansMultipleColumns()
    {
        var header = new TextBlockNode { Text = "Header" };
        var left = new TextBlockNode { Text = "Left" };
        var right = new TextBlockNode { Text = "Right" };

        var node = CreateGridNode(
            [
                new GridNode.CellEntry(header, Row: 0, Column: 0, RowSpan: 1, ColumnSpan: 2),
                new GridNode.CellEntry(left, Row: 1, Column: 0, RowSpan: 1, ColumnSpan: 1),
                new GridNode.CellEntry(right, Row: 1, Column: 1, RowSpan: 1, ColumnSpan: 1),
            ],
            columnCount: 2, rowCount: 2,
            colHints: [SizeHint.Fixed(30), SizeHint.Fixed(50)],
            rowHints: [SizeHint.Fixed(3), SizeHint.Fixed(5)]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Header spans both columns
        Assert.Equal(0, header.Bounds.X);
        Assert.Equal(80, header.Bounds.Width); // 30 + 50
        Assert.Equal(3, header.Bounds.Height);

        // Left in col 0
        Assert.Equal(0, left.Bounds.X);
        Assert.Equal(30, left.Bounds.Width);

        // Right in col 1
        Assert.Equal(30, right.Bounds.X);
        Assert.Equal(50, right.Bounds.Width);
    }

    #endregion

    #region GridCellWidget Fluent API Tests

    [Fact]
    public void GridCellWidget_Row_SetsRowAndDefaultSpan()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).Row(2);
        Assert.Equal(2, cell.RowIndex);
        Assert.Equal(1, cell.RowSpanCount);
    }

    [Fact]
    public void GridCellWidget_RowSpan_SetsRowAndSpan()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).RowSpan(1, 3);
        Assert.Equal(1, cell.RowIndex);
        Assert.Equal(3, cell.RowSpanCount);
    }

    [Fact]
    public void GridCellWidget_Column_SetsColumnAndDefaultSpan()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).Column(4);
        Assert.Equal(4, cell.ColumnIndex);
        Assert.Equal(1, cell.ColumnSpanCount);
    }

    [Fact]
    public void GridCellWidget_ColumnSpan_SetsColumnAndSpan()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).ColumnSpan(2, 3);
        Assert.Equal(2, cell.ColumnIndex);
        Assert.Equal(3, cell.ColumnSpanCount);
    }

    [Fact]
    public void GridCellWidget_Width_SetsFixedWidthHint()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).Width(25);
        Assert.Equal(SizeHint.Fixed(25), cell.CellWidthHint);
    }

    [Fact]
    public void GridCellWidget_FillWidth_SetsFillHint()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).FillWidth();
        Assert.Equal(SizeHint.Fill, cell.CellWidthHint);
    }

    [Fact]
    public void GridCellWidget_Height_SetsFixedHeightHint()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).Height(10);
        Assert.Equal(SizeHint.Fixed(10), cell.CellHeightHint);
    }

    [Fact]
    public void GridCellWidget_FillHeight_SetsFillHint()
    {
        var cell = new GridCellWidget(new TextBlockWidget("test")).FillHeight();
        Assert.Equal(SizeHint.Fill, cell.CellHeightHint);
    }

    #endregion

    #region GridColumnDefinition / GridRowDefinition Tests

    [Fact]
    public void GridColumnDefinition_DefaultConstructor_UsesContentSizing()
    {
        var def = new GridColumnDefinition();
        Assert.Equal(SizeHint.Content, def.Width);
    }

    [Fact]
    public void GridRowDefinition_DefaultConstructor_UsesContentSizing()
    {
        var def = new GridRowDefinition();
        Assert.Equal(SizeHint.Content, def.Height);
    }

    [Fact]
    public void GridDefinitionCollection_AddSizeHint_CreatesColumnDefinition()
    {
        var columns = new GridDefinitionCollection<GridColumnDefinition>();
        columns.Add(SizeHint.Fixed(20));
        Assert.Single(columns);
        Assert.Equal(SizeHint.Fixed(20), columns[0].Width);
    }

    [Fact]
    public void GridDefinitionCollection_AddSizeHint_CreatesRowDefinition()
    {
        var rows = new GridDefinitionCollection<GridRowDefinition>();
        rows.Add(SizeHint.Fill);
        Assert.Single(rows);
        Assert.Equal(SizeHint.Fill, rows[0].Height);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_ReturnsAllCellNodes()
    {
        var a = new TextBlockNode { Text = "A" };
        var b = new TextBlockNode { Text = "B" };
        var c = new TextBlockNode { Text = "C" };

        var node = CreateGridNode(
            [
                new GridNode.CellEntry(a, 0, 0, 1, 1),
                new GridNode.CellEntry(b, 0, 1, 1, 1),
                new GridNode.CellEntry(c, 1, 0, 1, 1),
            ],
            columnCount: 2, rowCount: 2,
            colHints: [SizeHint.Content, SizeHint.Content],
            rowHints: [SizeHint.Content, SizeHint.Content]);

        var children = node.GetChildren().ToList();

        Assert.Equal(3, children.Count);
        Assert.Contains(a, children);
        Assert.Contains(b, children);
        Assert.Contains(c, children);
    }

    #endregion

    #region Classic Layout Scenario

    [Fact]
    public void Arrange_ClassicHolyGrailLayout_PositionsCorrectly()
    {
        // Classic layout:
        // | Nav (20w) | Header  |
        // | Nav       | Content |
        var nav = new TextBlockNode { Text = "Nav" };
        var header = new TextBlockNode { Text = "Header" };
        var content = new TextBlockNode { Text = "Content" };

        var node = CreateGridNode(
            [
                new GridNode.CellEntry(nav, Row: 0, Column: 0, RowSpan: 2, ColumnSpan: 1),
                new GridNode.CellEntry(header, Row: 0, Column: 1, RowSpan: 1, ColumnSpan: 1),
                new GridNode.CellEntry(content, Row: 1, Column: 1, RowSpan: 1, ColumnSpan: 1),
            ],
            columnCount: 2, rowCount: 2,
            colHints: [SizeHint.Fixed(20), SizeHint.Fill],
            rowHints: [SizeHint.Fixed(3), SizeHint.Fill]);

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Nav: col 0, rows 0-1, 20w × 24h
        Assert.Equal(new Rect(0, 0, 20, 24), nav.Bounds);

        // Header: col 1, row 0, 60w × 3h
        Assert.Equal(new Rect(20, 0, 60, 3), header.Bounds);

        // Content: col 1, row 1, 60w × 21h
        Assert.Equal(new Rect(20, 3, 60, 21), content.Bounds);
    }

    #endregion

    #region Helpers

    private static GridNode CreateGridNode(
        GridNode.CellEntry[] entries,
        int columnCount,
        int rowCount,
        SizeHint[] colHints,
        SizeHint[] rowHints)
    {
        return new GridNode
        {
            CellEntries = entries.ToList(),
            ColumnCount = columnCount,
            RowCount = rowCount,
            EffectiveColumnHints = colHints,
            EffectiveRowHints = rowHints,
        };
    }

    #endregion
}
