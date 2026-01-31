ctx.Border(b => [
    b.VStack(v => [
        v.Text("Audio Settings"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Master:  ").FixedWidth(10),
            h.Slider(80).Fill()
        ]),
        v.HStack(h => [
            h.Text("Music:   ").FixedWidth(10),
            h.Slider(60).Fill()
        ]),
        v.HStack(h => [
            h.Text("Effects: ").FixedWidth(10),
            h.Slider(90).Fill()
        ])
    ])
], title: "Settings")
