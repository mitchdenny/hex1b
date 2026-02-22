ctx.Grid(g => [
    g.Cell(c => c.Text("A")).Row(0).Column(0).Width(10),
    g.Cell(c => c.Text("B")).Row(0).Column(1).FillWidth(),
    g.Cell(c => c.Text("C")).Row(0).Column(2).Width(10),
])
