ctx.VStack(v => [
    v.Float(v.Border(b => [
        b.Text("Top-Left")
    ]).Title("Panel A")).Absolute(0, 0),
    v.Float(v.Border(b => [
        b.Text("Top-Right")
    ]).Title("Panel B")).Absolute(25, 0),
    v.Float(v.Border(b => [
        b.Text("Overlapping!")
    ]).Title("Panel C")).Absolute(10, 6),
])
