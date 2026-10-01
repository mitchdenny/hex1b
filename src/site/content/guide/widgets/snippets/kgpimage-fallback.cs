// Fallback with a simple text widget
v.KgpImage(pixels, width, height,
    img => img.Text("A sunset over the ocean"))

// Fallback with a full widget tree for richer alternatives
v.KgpImage(pixels, width, height,
    img => img.Border(b => [
        b.Text("Image: sunset.png (KGP not supported)")
    ]).Title("Preview"))
