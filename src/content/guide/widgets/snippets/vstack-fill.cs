ctx.Border(b => [
    b.VStack(v => [
        v.Text("Header (content height)"),
        v.Border(b2 => [
            b2.Text("Main (fills remaining)")
        ]).Fill(),
        v.Text("Footer (content height)")
    ])
])
