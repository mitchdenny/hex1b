ctx.Responsive(r => [
    r.WhenMinWidth(80, r => 
        r.HStack(h => [
            h.Text("Left Panel"),
            h.Text(" | "),
            h.Text("Right Panel")
        ])
    ),
    r.Otherwise(r =>
        r.VStack(v => [
            v.Text("Top Panel"),
            v.Text("─────────"),
            v.Text("Bottom Panel")
        ])
    )
])
