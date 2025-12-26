<!--
  MIRROR WARNING: The code sample below must stay in sync with the WebSocket example:
  - basicCode → src/Hex1b.Website/Examples/SixelExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Sixel Graphics Demo"),
        v.Text(""),
        v.Sixel(
            sixelImageData,
            v.Text("[Sixel not supported]"),
            width: 40,
            height: 20
        )
    ])
));

await app.RunAsync();`

const fallbackCode = `using Hex1b;
using Hex1b.Widgets;

// Simple text fallback
v.Sixel(
    sixelData,
    "[Image not available]",
    width: 30,
    height: 15
)

// Widget fallback
v.Sixel(
    sixelData,
    v.Border(b => [
        b.Text("Sixel not supported"),
        b.Text("Use a compatible terminal")
    ], title: "Notice"),
    width: 30,
    height: 15
)

// VStack fallback builder
v.Sixel(
    sixelData,
    fallback => [
        fallback.Text("┌─────────────────────┐"),
        fallback.Text("│ Image not available │"),
        fallback.Text("└─────────────────────┘")
    ],
    width: 30,
    height: 15
)`
</script>

# SixelWidget

Display Sixel graphics in terminals that support the Sixel image protocol, with automatic fallback for unsupported terminals.

[Sixel](https://en.wikipedia.org/wiki/Sixel) is a bitmap graphics format originally developed for DEC terminals. Modern terminal emulators like xterm, WezTerm, mlterm, and foot support Sixel, enabling inline image display in terminal applications.

::: warning Experimental Feature
SixelWidget is currently an experimental feature marked with `[Experimental("HEX1B_SIXEL")]`. The API may change in future releases. Enable experimental features in your project to use this widget.
:::

## Basic Usage

Create a Sixel widget with image data and a fallback for unsupported terminals:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="sixel" exampleTitle="Sixel Widget - Basic Usage" />

The widget automatically detects Sixel support by querying the terminal's capabilities. If supported, it renders the Sixel image; otherwise, it displays the fallback widget.

::: tip Terminal Support Detection
Sixel support is detected using the DA1 (Primary Device Attributes) escape sequence. The terminal's response indicates whether it has graphics capability (`;4` in the response). Detection happens on first render with a 1-second timeout.
:::

## Image Data Format

The `imageData` parameter accepts raw Sixel-encoded data. Sixel data can include or omit the DCS (Device Control String) wrapper:

```csharp
// With DCS wrapper (ESC P q)
string sixelData = "\x1bPq...sixel data...\x1b\\";

// Without wrapper (automatically added)
string sixelData = "#0;2;0;0;0#1;2;100;100;0...";
```

The widget automatically adds the DCS wrapper if not present.

::: tip Encoding Images to Sixel
To convert common image formats (PNG, JPEG, SVG, etc.) to Sixel, you'll need an encoder. The repository includes `SixelEncoder.cs` in the Website project as an example implementation using ImageSharp and a third-party Sixel library.

For production use, consider:
- **ImageMagick**: `magick convert input.png -geometry 640x480 sixel:-`
- **libsixel**: Dedicated Sixel encoding library with C bindings
- **Custom encoder**: Use ImageSharp or SkiaSharp for image manipulation before Sixel encoding
:::

## Fallback Options

SixelWidget provides multiple ways to specify fallback content for terminals without Sixel support:

<CodeBlock lang="csharp" :code="fallbackCode" command="dotnet run" />

### Simple Text Fallback

The simplest approach uses a string that becomes a TextWidget:

```csharp
v.Sixel(sixelData, "[Image not available]")
```

### Widget Fallback

Pass any widget as the fallback:

```csharp
v.Sixel(
    sixelData,
    v.Border(b => [
        b.Text("📷 Image Preview"),
        b.Text(""),
        b.Text("Sixel graphics not supported"),
        b.Text("in this terminal.")
    ], title: "Notice")
)
```

### Builder Pattern Fallback

Use a builder function for VStack-based fallbacks:

```csharp
v.Sixel(
    sixelData,
    fallback => [
        fallback.Text("Image: photo.jpg"),
        fallback.Text("Size: 640x480"),
        fallback.Text(""),
        fallback.Text("Terminal does not support Sixel")
    ]
)
```

## Sizing

Control the image display size using the `width` and `height` parameters (in character cells):

```csharp
// Fixed size
v.Sixel(sixelData, fallback, width: 40, height: 20)

// Natural size (from image data)
v.Sixel(sixelData, fallback)

// Specify only width (height auto)
v.Sixel(sixelData, fallback, width: 60)
```

When dimensions aren't specified, the widget uses the image's natural size or defaults to 40x20 cells.

::: tip Cell-to-Pixel Conversion
Terminal cell size varies by font and terminal settings. A typical terminal cell is approximately 9×18 pixels (for a 14pt monospace font). The actual pixel dimensions of your Sixel image will be scaled to fit within the requested cell dimensions.
:::

## Layout Behavior

SixelWidget measures to accommodate either the Sixel image or the fallback widget, whichever is larger:

```csharp
ctx.VStack(v => [
    v.Text("Header"),
    v.Sixel(largeImage, fallback, width: 80, height: 30),
    v.Text("Footer")
])
```

During layout measurement (before terminal capabilities are known), the widget calculates bounds for both rendering modes to ensure sufficient space.

## Focus and Input

SixelWidget itself is not focusable and doesn't handle input. However, if your fallback widget contains focusable elements (like buttons), those will be accessible when the fallback is displayed:

```csharp
v.Sixel(
    sixelData,
    v.VStack(fallback => [
        fallback.Text("Image not available"),
        fallback.Button("Continue").OnClick(_ => /* ... */)
    ])
)
```

## Terminal Compatibility

Sixel support varies by terminal emulator:

| Terminal | Sixel Support | Notes |
|----------|---------------|-------|
| **xterm** | ✅ Full | Use `-ti vt340` flag for best results |
| **WezTerm** | ✅ Full | Native support, excellent performance |
| **mlterm** | ✅ Full | Strong Sixel implementation |
| **foot** | ✅ Full | Lightweight with Sixel support |
| **iTerm2** | ✅ Full | macOS terminal with inline images |
| **Konsole** | ⚠️ Partial | Basic support, may have rendering issues |
| **Windows Terminal** | ❌ None | No Sixel support (as of 2024) |
| **GNOME Terminal** | ❌ None | No Sixel support |
| **Alacritty** | ❌ None | No Sixel support |

::: tip Testing Sixel Support
To test if your terminal supports Sixel, run:
```bash
echo -e "\x1bPq#0;2;0;0;0#1;2;100;100;0#1~~@@vv@@~~@@~~$#2;2;0;100;0#2??}}GG}}??}}??-\x1b\\"
```

You should see a small colored image. If you see escape codes, Sixel is not supported.
:::

## Performance Considerations

Sixel rendering can be resource-intensive:

- **Cache encoded data**: Generate Sixel strings once and reuse them
- **Size appropriately**: Larger images take longer to encode and render
- **Consider fallbacks**: Ensure fallback widgets are lightweight

```csharp
// Cache Sixel data in state
class ImageState
{
    public string? CachedSixelData { get; set; }
    public int CachedWidth { get; set; }
    public int CachedHeight { get; set; }
}

var state = new ImageState();

// Regenerate only when size changes
if (state.CachedSixelData == null || 
    state.CachedWidth != requestedWidth ||
    state.CachedHeight != requestedHeight)
{
    state.CachedSixelData = EncodeSixel(imagePath, requestedWidth, requestedHeight);
    state.CachedWidth = requestedWidth;
    state.CachedHeight = requestedHeight;
}

v.Sixel(state.CachedSixelData, fallback, width: requestedWidth, height: requestedHeight)
```

## Common Use Cases

### Image Gallery

Display a collection of images with navigation:

```csharp
var images = new[] { "photo1.sixel", "photo2.sixel", "photo3.sixel" };
var selectedIndex = 0;

ctx.VStack(v => [
    v.Text($"Image {selectedIndex + 1} of {images.Length}"),
    v.Sixel(
        LoadSixelData(images[selectedIndex]),
        $"[{Path.GetFileName(images[selectedIndex])}]",
        width: 60,
        height: 30
    ),
    v.HStack(h => [
        h.Button("< Prev").OnClick(_ => selectedIndex = Math.Max(0, selectedIndex - 1)),
        h.Button("Next >").OnClick(_ => selectedIndex = Math.Min(images.Length - 1, selectedIndex + 1))
    ])
])
```

### Data Visualization

Render charts and graphs inline:

```csharp
// Generate chart as image, encode to Sixel
var chartSixelData = GenerateChartAsSixel(dataPoints);

ctx.Border(b => [
    b.Text("Sales Report - Q4 2024"),
    b.Text(""),
    b.Sixel(
        chartSixelData,
        b.Text("[Chart rendering not supported]"),
        width: 70,
        height: 25
    )
], title: "Dashboard")
```

### Logo or Branding

Display application branding:

```csharp
ctx.VStack(v => [
    v.Sixel(
        companyLogoSixel,
        v.Text("ACME Corporation"),
        width: 30,
        height: 10
    ),
    v.Text(""),
    v.Text("Welcome to the application...")
])
```

## Related Widgets

- [TextWidget](/guide/widgets/text) - For displaying text when images aren't available
- [BorderWidget](/guide/widgets/containers) - For framing images with decorative borders
- [Layout & Stacks](/guide/widgets/stacks) - For arranging images with other content
