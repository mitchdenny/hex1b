<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode  → src/Hex1b.Website/Examples/FigletBasicExample.cs
  - fontsCode  → src/Hex1b.Website/Examples/FigletFontsExample.cs
  - colorCode  → src/Hex1b.Website/Examples/FigletColorExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import fullWidthSnippet from './snippets/figlet-layout-fullwidth.cs?raw'
import fittedSnippet    from './snippets/figlet-layout-fitted.cs?raw'
import smushedSnippet   from './snippets/figlet-layout-smushed.cs?raw'
import clipSnippet      from './snippets/figlet-overflow-clip.cs?raw'
import wrapSnippet      from './snippets/figlet-overflow-wrap.cs?raw'
import galleryStaticSnippet from './snippets/figlet-fonts-gallery.cs?raw'
import customFontSnippet from './snippets/figlet-custom-font.cs?raw'
import subclassSnippet   from './snippets/figlet-subclass.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.FigletText("Hello").Font(FigletFonts.Standard),
        v.Text(""),
        v.Text("Press Ctrl+C to exit.")
    ]))
    .Build();

await terminal.RunAsync();`

const fontsCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("standard:"),
        v.FigletText("Hex1b").Font(FigletFonts.Standard),
        v.Text(""),
        v.Text("slant:"),
        v.FigletText("Hex1b").Font(FigletFonts.Slant),
        v.Text(""),
        v.Text("small:"),
        v.FigletText("Hex1b").Font(FigletFonts.Small),
    ]))
    .Build();

await terminal.RunAsync();`

const colorCode = `using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.EffectPanel(
            v.FigletText("Hex1b").Font(FigletFonts.Slant),
            surface =>
            {
                // Apply a horizontal gradient to non-blank cells.
                var start = (R: (byte)64,  G: (byte)156, B: (byte)255);
                var end   = (R: (byte)255, G: (byte)128, B: (byte)64);
                for (var y = 0; y < surface.Height; y++)
                for (var x = 0; x < surface.Width; x++)
                {
                    var cell = surface[x, y];
                    if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
                    var t = (double)x / Math.Max(1, surface.Width - 1);
                    var r = (byte)(start.R + (end.R - start.R) * t);
                    var g = (byte)(start.G + (end.G - start.G) * t);
                    var b = (byte)(start.B + (end.B - start.B) * t);
                    surface[x, y] = cell with { Foreground = Hex1bColor.FromRgb(r, g, b) };
                }
            }),
        v.Text(""),
        v.Text("FigletText is monochrome — apply colors with EffectPanel.")
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# FigletText

Display large ASCII-art text rendered from a [FIGfont](http://www.jave.de/figlet/figfont.html). Useful for splash screens, banners, and any place a normal `Text` widget feels too understated.

## Basic Usage

Use `FigletText(...)` and pick a font from the bundled [`FigletFonts`](#choosing-a-font) catalog:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="figlet-basic" exampleTitle="FigletText Widget - Basic Usage" />

`FigletText` measures to the natural width and height of the rendered glyphs, so you can compose it inside any layout container just like an ordinary widget.

## Choosing a Font

Hex1b ships eight royalty-free FIGfonts as embedded resources, exposed as lazily-loaded singletons on `FigletFonts`:

| Property | Description |
|---|---|
| `FigletFonts.Standard` | The default monospace banner font from the FIGlet distribution |
| `FigletFonts.Slant` | Italicized variant of standard |
| `FigletFonts.Small` | Compact 5-row variant for narrower terminals |
| `FigletFonts.Big` | Enlarged 8-row banner font |
| `FigletFonts.Mini` | Tiny 4-row font for extremely narrow output |
| `FigletFonts.Shadow` | Text with a drop-shadow effect |
| `FigletFonts.Block` | Solid block letters |
| `FigletFonts.Banner` | Wide hash-shaped letters reminiscent of `banner(1)` |

Pass any of them to `.Font(...)`:

<CodeBlock lang="csharp" :code="fontsCode" command="dotnet run" example="figlet-fonts" exampleTitle="FigletText Widget - Bundled Fonts" />

<StaticTerminalPreview svgPath="/svg/figlet-fonts-gallery.svg" :code="galleryStaticSnippet" />

::: tip
Loading bundled fonts via the `FigletFonts.*` properties is essentially free after the first access — each font is parsed once and cached for the lifetime of the process. Avoid `FigletFont.LoadBundled(string)` in render paths because it re-parses the resource on every call.
:::

## Layout Modes

FIGfonts can be composed in three different ways. The same `FigletLayoutMode` enum is used for both axes — apply it via `.Layout(mode)` for both, or `.Horizontal(mode)` / `.Vertical(mode)` to set them independently.

| Mode | Behavior |
|---|---|
| `Default` | Defer to whatever layout mode the font's header declares (usually smushing). |
| `FullWidth` | Concatenate glyphs at their natural width with no overlap. Widest output. |
| `Fitted` | Slide glyphs together until any non-space cells touch (kerning). |
| `Smushed` | Overlap glyphs by the maximum amount allowed by the font's smushing rules. Tightest output. |

### FullWidth

<StaticTerminalPreview svgPath="/svg/figlet-layout-fullwidth.svg" :code="fullWidthSnippet" />

### Fitted

<StaticTerminalPreview svgPath="/svg/figlet-layout-fitted.svg" :code="fittedSnippet" />

### Smushed

<StaticTerminalPreview svgPath="/svg/figlet-layout-smushed.svg" :code="smushedSnippet" />

::: info Vertical layout
Vertical layout only affects output that has multiple FIGlet rows (input containing `\n` characters or wrapped paragraphs). For single-line text, the vertical mode has no observable effect.
:::

## Overflow Behavior

FIGcharacters are typically much wider than ordinary text, so even short strings can overflow narrow containers. `FigletText` exposes independent horizontal and vertical overflow policies.

### Horizontal: Clip vs Wrap

| Mode | Behavior |
|---|---|
| `Clip` *(default)* | Render at the natural unwrapped width; the parent clips overflow at the right edge. |
| `Wrap` | Word-wrap the input on whitespace boundaries so each rendered FIGlet block fits the available width. Single tokens that exceed the width are broken at character boundaries. |

#### Clip

<StaticTerminalPreview svgPath="/svg/figlet-overflow-clip.svg" :code="clipSnippet" />

#### Wrap

<StaticTerminalPreview svgPath="/svg/figlet-overflow-wrap.svg" :code="wrapSnippet" />

::: warning
The wrap algorithm is intentionally not byte-compatible with reference FIGlet's `-w` CLI option. The FIGfont 2.0 specification does not define line wrapping, so implementations differ. Hex1b's wrapper greedily fits whitespace-separated tokens and breaks overlong words at character boundaries.
:::

### Vertical: Clip vs Truncate

| Mode | Behavior |
|---|---|
| `Clip` *(default)* | Render at full height; the parent clips at the bottom, possibly leaving a partial FIGlet row visible. |
| `Truncate` | Drop entire FIGlet rows that don't fully fit. Never emits a partial row. |

```csharp
v.FigletText(longMultiLineText)
    .VerticalOverflow(FigletVerticalOverflow.Truncate)
```

Use `Truncate` when partial rows of glyphs would look broken (for example inside a fixed-height status panel).

## Adding Color and Animation

FIGlet text is rendered monochrome by design — the widget produces a grid of sub-characters and applies no styling of its own. To colorize the output, wrap the widget in an [`EffectPanel`](/guide/widgets/effectpanel) and supply a per-frame surface filter:

<CodeBlock lang="csharp" :code="colorCode" command="dotnet run" example="figlet-color" exampleTitle="FigletText Widget - Color via EffectPanel" />

The effect callback runs on every render and gets full read/write access to the rendered cells, so you can also implement rainbows, waves, shimmer, or any other post-processing pass without modifying the underlying FIGlet output.

## Using Custom Fonts

To use a font that isn't bundled, load any `.flf` file at startup with `FigletFont.LoadFileAsync` (or `LoadAsync` from any `Stream`) and pass the resulting font to `.Font(...)`:

<StaticTerminalPreview svgPath="/svg/figlet-fonts-gallery.svg" :code="customFontSnippet" />

`FigletFont` instances are immutable and safe to share across threads. Cache them at application startup — parsing is moderately expensive and there's no benefit to re-parsing on each frame.

## Subclassing FigletFont

`FigletFont` is intentionally extensible. Override `TryGetGlyph(int, out FigletGlyph)` to substitute, decorate, or lazily generate glyphs — this is the hook for fallback chains, character substitution, or fully synthetic fonts:

<StaticTerminalPreview svgPath="/svg/figlet-fonts-gallery.svg" :code="subclassSnippet" />

Two constructors are available for subclasses:

- `protected FigletFont(FigletFont inner)` — decorator constructor; delegates everything to `inner` until you override.
- `protected FigletFont(int height, int baseline, char hardblank, ...)` — primitive constructor for fully synthetic fonts that don't wrap an existing one.

You can also override `GetMissingGlyph()` to provide a custom appearance for characters that aren't present in the font (the default returns the font's space glyph, matching reference figlet behavior).

## Related Widgets

- [Text](/guide/widgets/text) — Ordinary text rendering for body content
- [EffectPanel](/guide/widgets/effectpanel) — Apply post-processing effects (color, gradients, animations) to any rendered subtree
