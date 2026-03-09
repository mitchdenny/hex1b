// Stretched (default) — fills the area, may distort
v.KgpImage(pixels, w, h, "fallback").Stretched()

// Fit — preserves aspect ratio, may leave empty space
v.KgpImage(pixels, w, h, "fallback").Fit()

// Fill — preserves aspect ratio, crops excess
v.KgpImage(pixels, w, h, "fallback").Fill()

// NaturalSize — native pixel-to-cell dimensions
v.KgpImage(pixels, w, h, "fallback").NaturalSize()
