ctx.VStack(v => [
    v.Text("Focused:"),
    v.TextBox("cursor here"),    // ← Cursor visible
    v.Text(""),
    v.Text("Unfocused:"),
    v.TextBox("no cursor")
])
