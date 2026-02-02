<script setup>
const basicCode = `using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Surface(s => [
        s.Layer(surface => {
            // Draw a simple pattern
            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    var isCheckerboard = (x + y) % 2 == 0;
                    surface[x, y] = SurfaceCells.Char(
                        isCheckerboard ? '░' : '▓',
                        isCheckerboard ? Hex1bColor.DarkGray : Hex1bColor.Gray
                    );
                }
            }
        })
    ]).Size(20, 10))
    .Build();

await terminal.RunAsync();`

const layersCode = `using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Surface(s => [
        // Layer 1: Background gradient
        s.Layer(surface => {
            for (int y = 0; y < surface.Height; y++)
            {
                var shade = (byte)(50 + y * 10);
                for (int x = 0; x < surface.Width; x++)
                {
                    surface[x, y] = SurfaceCells.Space(Hex1bColor.FromRgb(0, 0, shade));
                }
            }
        }),
        // Layer 2: Text overlay
        s.Layer(surface => {
            var text = "SURFACE";
            var startX = (surface.Width - text.Length) / 2;
            var y = surface.Height / 2;
            for (int i = 0; i < text.Length; i++)
            {
                surface[startX + i, y] = SurfaceCells.Char(
                    text[i], Hex1bColor.White
                );
            }
        })
    ]).Size(30, 10))
    .Build();

await terminal.RunAsync();`

const mouseCode = `using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.Surface(s => [
        // Background
        s.Layer(surface => {
            for (int y = 0; y < surface.Height; y++)
                for (int x = 0; x < surface.Width; x++)
                    surface[x, y] = SurfaceCells.Char('·', Hex1bColor.DarkGray);
        }),
        // Mouse highlight using computed layer
        s.Layer(computeCtx => {
            // Only draw if mouse is over the surface
            if (s.MouseX < 0 || s.MouseY < 0)
                return computeCtx.GetBelow();  // Pass through
            
            // Highlight area around mouse
            var dx = Math.Abs(computeCtx.X - s.MouseX);
            var dy = Math.Abs(computeCtx.Y - s.MouseY);
            if (dx <= 2 && dy <= 1)
            {
                var below = computeCtx.GetBelow();
                return below
                    .WithForeground(Hex1bColor.Yellow)
                    .WithBackground(Hex1bColor.Blue);
            }
            return computeCtx.GetBelow();
        })
    ]).Size(30, 10))
    .Build();

await terminal.RunAsync();`
</script>

# Surface

The Surface widget provides direct access to Hex1b's low-level rendering API, enabling arbitrary visualizations with multiple composited layers. Use it for custom graphics, game rendering, data visualization, or any UI that goes beyond standard widgets.

## Basic Usage

Create a surface using `Surface()` with a layer builder that returns one or more layers:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="surface-basic" exampleTitle="Surface - Basic Usage" />

The layer builder receives a `SurfaceLayerContext` that provides:
- Factory methods for creating layers (`Layer()`)
- Mouse position (`MouseX`, `MouseY`)
- Surface dimensions (`Width`, `Height`)
- Theme access for styled effects

## Layer Types

Surfaces support three types of layers, composited bottom-up:

| Layer Type | Created With | Use Case |
|------------|-------------|----------|
| **Source** | `s.Layer(ISurfaceSource)` | Pre-rendered or externally managed surfaces |
| **Draw** | `s.Layer(surface => { ... })` | Dynamic content drawn each frame |
| **Computed** | `s.Layer(ctx => ...)` | Effects that depend on layers below |

### Draw Layers

Draw layers receive a fresh `Surface` each render. Use the indexer to set cells:

```csharp
s.Layer(surface => {
    surface[x, y] = new SurfaceCell(
        character,      // The character to display
        foreground,     // Foreground color (null = transparent)
        background      // Background color (null = transparent)
    );
})
```

### Compositing Multiple Layers

Layers composite bottom-up. Later layers draw on top of earlier ones:

<CodeBlock lang="csharp" :code="layersCode" command="dotnet run" example="surface-layers" exampleTitle="Surface - Multiple Layers" />

### Computed Layers

Computed layers calculate each cell based on layers below, enabling effects like fog of war, tinting, or shadows:

```csharp
s.Layer(ctx => {
    var below = ctx.GetBelow();  // Get the composited cell from layers below
    
    // Apply a color tint by replacing the foreground
    return below.WithForeground(Hex1bColor.Red);
})
```

The `ComputeContext` provides:
- `X`, `Y` - Current cell position
- `GetBelow()` - Get the composited cell from all layers below
- `Width`, `Height` - Surface dimensions

## Mouse Interaction

The layer context provides mouse position for interactive effects:

<CodeBlock lang="csharp" :code="mouseCode" command="dotnet run" example="surface-mouse" exampleTitle="Surface - Mouse Interaction" />

::: tip Enable Mouse
Call `.WithMouse()` on the terminal builder to receive mouse position updates.
:::

## Sizing

By default, surfaces fill all available space. Control sizing with:

```csharp
// Fixed size
ctx.Surface(...).Size(40, 20)

// Custom hints
ctx.Surface(...)
   .Width(SizeHint.Fixed(40))
   .Height(SizeHint.Content)
```

## SurfaceCell Properties

Each cell in a surface can have:

| Property | Type | Description |
|----------|------|-------------|
| `Character` | `char` | The character to display |
| `Foreground` | `Hex1bColor?` | Text color (null = transparent) |
| `Background` | `Hex1bColor?` | Background color (null = transparent) |
| `Sixel` | `TrackedObject<SixelData>?` | Sixel graphics data |

## Use Cases

Surfaces are ideal for:

| Scenario | Approach |
|----------|----------|
| Game rendering | Multiple layers: terrain, entities, UI, effects |
| Data visualization | Draw charts, graphs, heatmaps |
| Custom animations | Update layer content each frame |
| Image display | Use sixel graphics with `CreateSixel()` |
| Fog of war | Computed layer that masks based on visibility |

## Sixel Graphics

Create sixel graphics for image display:

```csharp
s.Layer(surface => {
    // Create a pixel buffer (width x height in pixels)
    var pixels = new SixelPixelBuffer(100, 50);
    
    // Draw pixels
    for (int py = 0; py < pixels.Height; py++)
        for (int px = 0; px < pixels.Width; px++)
            pixels[px, py] = Rgba32.FromRgb((byte)px, (byte)py, 128);
    
    // Create tracked sixel and place it
    var sixel = s.CreateSixel(pixels);
    if (sixel != null)
    {
        surface[0, 0] = new SurfaceCell { Sixel = sixel };
    }
})
```

::: warning Terminal Support
Sixel graphics require terminal support. Not all terminals support sixels.
:::

## Performance Tips

1. **Minimize computed layers** - They run per-cell per-frame
2. **Use source layers** for static content - Create once, reuse
3. **Cache surfaces** when content doesn't change
4. **Keep layer count low** - Each layer adds compositing overhead

## Related

- [Layout System](/guide/layout) - How sizing works
- [Theming](/guide/theming) - Access colors via `s.Theme`
