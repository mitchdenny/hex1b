<!--
  ⚠️ MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/KgpImageBasicExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import stretchSnippet from './snippets/kgpimage-stretch.cs?raw'
import fallbackSnippet from './snippets/kgpimage-fallback.cs?raw'

const basicCode = `using Hex1b;

// Generate a simple gradient image (RGBA32 format: 4 bytes per pixel)
var width = 64;
var height = 32;
var pixels = new byte[width * height * 4];
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        var i = (y * width + x) * 4;
        pixels[i]     = (byte)(x * 255 / width);   // R
        pixels[i + 1] = (byte)(y * 255 / height);  // G
        pixels[i + 2] = 128;                        // B
        pixels[i + 3] = 255;                        // A
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("KGP Image Demo"),
        v.KgpImage(pixels, width, height, "Terminal does not support graphics")
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# KgpImage

Display pixel-based images in terminals that support the [Kitty Graphics Protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/) (KGP). When the terminal does not support KGP, a fallback widget is rendered instead.

::: tip Terminal Support
KGP is supported by [Kitty](https://sw.kovidgoyal.net/kitty/), [WezTerm](https://wezfurlong.org/wezterm/), and other terminals implementing the protocol. Your application should always provide a meaningful fallback for terminals without graphics support.
:::

## Basic Usage

Create a KGP image from raw RGBA32 pixel data with a text fallback:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="kgpimage-basic" exampleTitle="KGP Image - Basic Usage" />

The image data must be in **RGBA32 format** — 4 bytes per pixel (red, green, blue, alpha), row-major order. You must also provide the pixel dimensions so the protocol can transmit the image correctly.

## Fallback Content

Every `KgpImage` requires a fallback — a widget displayed when the terminal does not support KGP. You can provide either a string or a full widget:

<StaticTerminalPreview svgPath="/svg/kgpimage-fallback.svg" :code="fallbackSnippet" />

::: warning Always Provide Useful Fallbacks
Don't use empty strings or placeholder text. The fallback is what most terminal users will see — make it informative. Consider using ASCII art, a description of the image, or alternative UI elements.
:::

## Stretch Modes

Control how the image fills its allocated area using stretch modes:

<StaticTerminalPreview svgPath="/svg/kgpimage-stretch.svg" :code="stretchSnippet" />

| Mode | Method | Behavior |
|------|--------|----------|
| **Stretch** | `.Stretched()` | Fills the area completely, distorting the aspect ratio. This is the default. |
| **Fit** | `.Fit()` | Scales to fit within the area while preserving aspect ratio. May leave empty space. |
| **Fill** | `.Fill()` | Scales to cover the entire area, preserving aspect ratio. Excess is cropped. |
| **None** | `.NaturalSize()` | Displays at native pixel-to-cell dimensions without any scaling. |

## Sizing

By default, the image size is calculated from the pixel dimensions (roughly 10 pixels per column, 20 pixels per row). You can override the display size in character cells:

```csharp
// Explicit size in character cells
v.KgpImage(pixels, width, height, "fallback")
    .WithWidth(20)
    .WithHeight(10)

// Or set size at creation time
v.KgpImage(pixels, width, height, "fallback", width: 20, height: 10)
```

Use layout size hints to make the image fill available space:

```csharp
// Fill the parent container
v.KgpImage(pixels, width, height, "fallback")
    .Width(SizeHint.Fill)
    .Height(SizeHint.Fill)
```

## Z-Ordering

KGP images can be rendered above or below the text layer:

```csharp
// Render behind text (default)
v.KgpImage(pixels, width, height, "fallback").BelowText()

// Render on top of text
v.KgpImage(pixels, width, height, "fallback").AboveText()
```

When placed below text, the image acts as a background — any text widgets overlapping the image area will be visible on top.

## Loading Images from Files

For real-world usage, you'll typically load images from files. Use a library like [SkiaSharp](https://github.com/mono/SkiaSharp) or [ImageSharp](https://github.com/SixLabors/ImageSharp) to decode images into RGBA pixel data:

```csharp
using SkiaSharp;

// Load and decode an image file
using var bitmap = SKBitmap.Decode("photo.png");
var pixels = bitmap.Bytes; // RGBA32 data
var w = bitmap.Width;
var h = bitmap.Height;

// Use in your widget tree
v.KgpImage(pixels, w, h, "Photo description")
    .Fit()
    .Width(SizeHint.Fill)
```

## Using with Surfaces

KGP images integrate with the [Surface](/guide/widgets/surface) compositing system. When images are rendered inside surface layers, the framework automatically handles:

- **Clipping** — Images are cropped to their container bounds
- **Occlusion** — Text or other widgets in higher layers correctly obscure portions of the image
- **Multi-placement** — A single image may be split into multiple visible fragments when partially occluded

This means KGP images work correctly inside scrollable areas, bordered containers, windows, and any other layout widget.

## Related Widgets

- [Surface](/guide/widgets/surface) — Low-level rendering with layered compositing
- [Text](/guide/widgets/text) — For text-based alternatives to images
