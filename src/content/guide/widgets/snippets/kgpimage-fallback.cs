// Fallback with a simple text string
v.KgpImage(pixels, width, height, "A sunset over the ocean")

// Fallback with a full widget for richer alternatives
v.KgpImage(pixels, width, height,
    v.Border(b => [
        b.Text("Image: sunset.png (KGP not supported)")
    ]).Title("Preview"))
