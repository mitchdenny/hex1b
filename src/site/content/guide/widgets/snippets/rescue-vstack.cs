ctx.Rescue(v => [
    v.Text("Some content"),
    v.Button("Click me").OnClick(_ => DoSomething())
])
