ctx.Rescue(
    ctx.SomeWidget()
)
.WithFallback(rescue => rescue.VStack(v => [
    v.Text("Oops! Something went wrong."),
    v.Text(""),
    v.Text($"Error: {rescue.Exception.Message}"),
    v.Text(""),
    v.Button("Try Again").OnClick(_ => rescue.Reset())
]))
