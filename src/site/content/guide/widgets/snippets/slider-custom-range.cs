ctx.VStack(v => [
    v.Text("Temperature (-10°C to 40°C)"),
    v.Slider(initialValue: 22, min: -10, max: 40)
])
