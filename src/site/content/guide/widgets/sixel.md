<!--
  MIRROR WARNING: The code sample below mirrors:
  - src/Hex1b.Website/Examples/SixelExample.cs
  - samples/SixelWidgetDemo/Program.cs
  Keep the public API usage in sync when making changes.
-->
<script setup>
const basicCode = `#pragma warning disable HEX1B_SIXEL

using Hex1b;
using Hex1b.Surfaces;

var pixels = new SixelPixelBuffer(160, 80);
for (var y = 0; y < pixels.Height; y++)
{
    for (var x = 0; x < pixels.Width; x++)
    {
        pixels[x, y] = Rgba32.FromRgb(
            (byte)(x * 255 / pixels.Width),
            (byte)(y * 255 / pixels.Height),
            180);
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx =>
        ctx.Sixel(
                pixels,
                fallback => fallback.Text("Sixel graphics are unavailable."))
            .Width(24)
            .Height(8))
    .Build();

await terminal.RunAsync();`
</script>

# SixelWidget

`SixelWidget` displays pixel graphics in terminals with affirmative Sixel
support and composes a normal fallback widget everywhere else. The API is
experimental and emits warning `HEX1B_SIXEL`.

## Structured pixels

Use `SixelPixelBuffer` for application-generated images. Hex1b encodes the
buffer, measures its natural size using the terminal's authoritative Sixel cell
metrics, and stores structured graphics when rendering through a `Surface`.

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="sixel" exampleTitle="Sixel Widget" />

Omit `Width(...)` and `Height(...)` to use the natural pixel-to-cell size. For
structured pixels, setting either dimension resamples the emitted raster to the
widget's arranged cell span using the active Sixel protocol metrics.

## Pre-encoded compatibility

Use the string overload only when you already have Sixel data:

```csharp
var image = ctx.Sixel(
    encodedSixel,
    fallback => fallback.Text("Sixel graphics are unavailable."));
```

The input may be a complete 7-bit or 8-bit DCS sequence, or an unframed Sixel
body. Hex1b validates it and normalizes output to one complete 7-bit
`ESC P ... ESC \` sequence. Malformed or incomplete data throws
`ArgumentException`.

Pre-encoded payloads remain lossless and are not resampled. Their arranged cell
span must match their natural span under the active Sixel protocol metrics. If
you apply `Width(...)` or `Height(...)`, use the payload's exact natural
dimension; an incompatible span throws `ArgumentException` when rendered. Use
`SixelPixelBuffer` when the image needs to resize with layout.

## Capability and fallback behavior

Native and headless Sixel presentations render the image. Unsupported and
unknown presentations render the fallback, which supplies measurement, focus,
input, and rendering behavior like any other widget subtree. The legacy
`SupportsSixel` capability remains compatible when typed Sixel support is
unknown.

## Surface behavior

Surface-backed applications retain Sixel as structured content instead of
parsing raw DCS written through the generic text path. This enables
deterministic replacement, movement, removal, clipping, resize, caching, and
text overlap. Overlapping text is emitted with visible image fragments around
the occluded cells.

For a larger native-terminal exercise, run:

```bash
dotnet run --project samples/SixelWidgetDemo
```

## Related widgets

- [KgpImageWidget](/guide/widgets/kgpimage) - Pixel images using the Kitty Graphics Protocol
- [SurfaceWidget](/guide/widgets/surface) - Low-level layered cell rendering
