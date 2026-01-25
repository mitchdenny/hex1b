ctx.VStack(v => [
    v.Text("Without quiet zone:"),
    v.QrCode("https://example.com")
        .WithQuietZone(0)
])
