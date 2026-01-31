ctx.VStack(v => [
    v.Text("Volume (steps of 10)"),
    v.Slider(initialValue: 50, min: 0, max: 100, step: 10)
])
