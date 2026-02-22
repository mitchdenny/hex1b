ctx.FloatPanel(f => [
    f.Place(0, 0, f.Border(b => [
        b.Text("Top-Left")
    ]).Title("Panel A")),
    f.Place(25, 0, f.Border(b => [
        b.Text("Top-Right")
    ]).Title("Panel B")),
    f.Place(10, 6, f.Border(b => [
        b.Text("Overlapping!")
    ]).Title("Panel C")),
])
