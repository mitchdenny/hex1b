ctx.Grid(g => [
    g.Cell(c => c.Text("Nav")).RowSpan(0, 2).Column(0).Width(20),
    g.Cell(c => c.Text("Header")).Row(0).Column(1),
    g.Cell(c => c.Text("Content")).Row(1).Column(1),
])
