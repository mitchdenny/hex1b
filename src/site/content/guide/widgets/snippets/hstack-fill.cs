ctx.Border(b => [
    b.HStack(h => [
        h.Border(b2 => [
            b2.Text("Fixed")
        ]).FixedWidth(15),
        h.Text(" "),
        h.Border(b2 => [
            b2.Text("Fills remaining space")
        ]).Fill()
    ])
])
