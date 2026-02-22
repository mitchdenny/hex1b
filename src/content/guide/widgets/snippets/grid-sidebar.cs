ctx.Grid(g => {
    g.Columns.Add(SizeHint.Fixed(20));
    g.Columns.Add(SizeHint.Fill);

    g.Rows.Add(SizeHint.Fixed(3));
    g.Rows.Add(SizeHint.Fill);

    return [
        g.Cell(c => c.Border(b => [
            b.Text("Sidebar")
        ]).Title("Nav")).RowSpan(0, 2).Column(0),
        g.Cell(c => c.Text("Header area")).Row(0).Column(1),
        g.Cell(c => c.Border(b => [
            b.Text("Main content")
        ]).Title("Content")).Row(1).Column(1),
    ];
})
