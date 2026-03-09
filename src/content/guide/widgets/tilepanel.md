<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/TilePanelBasicExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import datasourceSnippet from './snippets/tilepanel-datasource.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

var state = new MapState();
var dataSource = new GridTileDataSource();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Camera: ({state.CameraX:F1}, {state.CameraY:F1})  Zoom: {state.ZoomLevel}"),
        v.TilePanel(dataSource, state.CameraX, state.CameraY, state.ZoomLevel)
            .OnPan(e =>
            {
                state.CameraX += e.DeltaX;
                state.CameraY += e.DeltaY;
            })
            .OnZoom(e => state.ZoomLevel = e.NewZoomLevel)
    ]))
    .Build();

await terminal.RunAsync();

class MapState
{
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public int ZoomLevel { get; set; }
}

class GridTileDataSource : ITileDataSource
{
    public Size TileSize => new(3, 1);

    public ValueTask<TileData[,]> GetTilesAsync(
        int tileX, int tileY, int tilesWide, int tilesTall,
        CancellationToken cancellationToken = default)
    {
        var tiles = new TileData[tilesWide, tilesTall];
        for (int y = 0; y < tilesTall; y++)
        {
            for (int x = 0; x < tilesWide; x++)
            {
                var tx = tileX + x;
                var ty = tileY + y;
                var isEven = (tx + ty) % 2 == 0;
                tiles[x, y] = new TileData(
                    FormatCoord(tx, ty),
                    isEven ? Hex1bColor.FromRgb(100, 180, 255) : Hex1bColor.FromRgb(180, 180, 180),
                    isEven ? Hex1bColor.FromRgb(20, 40, 80) : Hex1bColor.FromRgb(30, 50, 30));
            }
        }
        return ValueTask.FromResult(tiles);
    }

    static string FormatCoord(int x, int y)
    {
        var s = $"{x},{y}";
        return s.Length <= 3 ? s.PadRight(3) : s[..3];
    }
}`

const poiCode = `using Hex1b;
using Hex1b.Data;
using Hex1b.Widgets;

var state = new MapState();
var dataSource = new GridTileDataSource();

var pois = new List<TilePointOfInterest>
{
    new(0, 0, "📍", "Origin"),
    new(5, 3, "🏠", "Home"),
    new(-3, -2, "⭐", "Star"),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Camera: ({state.CameraX:F1}, {state.CameraY:F1})"),
        v.TilePanel(dataSource, state.CameraX, state.CameraY, state.ZoomLevel)
            .WithPointsOfInterest(pois)
            .OnPan(e =>
            {
                state.CameraX += e.DeltaX;
                state.CameraY += e.DeltaY;
            })
            .OnZoom(e => state.ZoomLevel = e.NewZoomLevel)
            .OnPoiClicked(e =>
            {
                // Center camera on the clicked point of interest
                state.CameraX = e.PointOfInterest.X;
                state.CameraY = e.PointOfInterest.Y;
            })
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# TilePanelWidget

An infinite, pannable and zoomable tile map that renders content from a pluggable data source.

TilePanel displays a scrollable grid of tiles fetched from an `ITileDataSource`. Users navigate with arrow keys, zoom with +/- or mouse scroll, and can interact with clickable points of interest overlaid on the map. It follows a **controlled component** pattern — you own the camera state and update it in response to events.

## Basic Usage

Create a TilePanel by providing an `ITileDataSource` and wiring up pan/zoom handlers:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="tilepanel-basic" exampleTitle="TilePanel Widget - Basic Usage" />

::: tip Navigation
Use **arrow keys** to pan, **+/-** to zoom, and **Home** to reset. Mouse drag pans the map, and scroll wheel zooms toward the cursor position.
:::

## Creating a Data Source

TilePanel gets its content from an `ITileDataSource` implementation. The interface has two members:

- `TileSize` — the dimensions of each tile in characters at zoom level 0
- `GetTilesAsync()` — fetches a rectangular region of tiles

Here's a minimal data source that renders a coordinate grid:

<StaticTerminalPreview :code="datasourceSnippet" />

::: tip Async Loading
`GetTilesAsync` is called on a background thread, never blocking the UI. While tiles load, empty placeholders are shown. When tiles arrive, the panel automatically redraws.
:::

### TileData

Each tile is represented by a `TileData` record struct:

```csharp
public readonly record struct TileData(
    string Content,       // Text content to render
    Hex1bColor Foreground, // Foreground color
    Hex1bColor Background  // Background color
);
```

The `Content` string length should match `TileSize.Width`. When zooming, content is automatically scaled to fill the effective tile width.

## Camera Control

TilePanel uses a **controlled component** pattern. You own the camera state and update it via event handlers:

```csharp
var cameraX = 0.0;
var cameraY = 0.0;
var zoom = 0;

ctx.TilePanel(dataSource, cameraX, cameraY, zoom)
    .OnPan(e =>
    {
        cameraX += e.DeltaX;
        cameraY += e.DeltaY;
    })
    .OnZoom(e => zoom = e.NewZoomLevel)
```

### Pan Events

`OnPan` fires when the user presses arrow keys or drags the mouse. The `TilePanelPanEventArgs` provides:

| Property | Type | Description |
|----------|------|-------------|
| `DeltaX` | `double` | Horizontal pan delta in tile units |
| `DeltaY` | `double` | Vertical pan delta in tile units |

Arrow keys produce integer deltas (1 for normal, 5 for Shift+Arrow). Mouse drag produces fractional deltas based on pixel distance and current zoom.

### Zoom Events

`OnZoom` fires when the user presses +/- keys or scrolls the mouse wheel. The `TilePanelZoomEventArgs` provides:

| Property | Type | Description |
|----------|------|-------------|
| `NewZoomLevel` | `int` | The suggested zoom level after the change |
| `Delta` | `int` | The zoom delta (+1 or -1) |
| `PivotX` | `double` | Tile-space X coordinate of the zoom pivot |
| `PivotY` | `double` | Tile-space Y coordinate of the zoom pivot |

For mouse scroll, the pivot is at the cursor position — useful for implementing zoom-toward-cursor:

```csharp
.OnZoom(e =>
{
    // Adjust camera to keep the pivot point stable
    var scale = Math.Pow(2, e.Delta);
    cameraX = e.PivotX + (cameraX - e.PivotX) / scale;
    cameraY = e.PivotY + (cameraY - e.PivotY) / scale;
    zoom = e.NewZoomLevel;
})
```

### Zoom Levels

Each zoom level doubles or halves the tile render size:

| ZoomLevel | Scale | Effect |
|-----------|-------|--------|
| -2 | 0.25x | Quarter size tiles |
| -1 | 0.5x | Half size tiles |
| 0 | 1x | Base size (from `TileSize`) |
| 1 | 2x | Double size tiles |
| 2 | 4x | Quadruple size tiles |

Zoom is internally clamped to the range **-4 to 8**.

## Points of Interest

Overlay clickable markers on the tile map using `WithPointsOfInterest`:

```csharp
var pois = new List<TilePointOfInterest>
{
    new(0, 0, "📍", "Origin"),           // X, Y, Icon, Label
    new(5, 3, "🏠", "Home", myData),     // Optional Tag for user data
};

ctx.TilePanel(dataSource, cameraX, cameraY, zoom)
    .WithPointsOfInterest(pois)
    .OnPoiClicked(e =>
    {
        // Navigate to the clicked POI
        cameraX = e.PointOfInterest.X;
        cameraY = e.PointOfInterest.Y;
    })
```

POIs outside the visible viewport are automatically excluded from rendering. The `TilePointOfInterest` record provides:

| Property | Type | Description |
|----------|------|-------------|
| `X` | `double` | X coordinate in tile space |
| `Y` | `double` | Y coordinate in tile space |
| `Icon` | `string` | Icon character or emoji to display |
| `Label` | `string?` | Optional text label near the icon |
| `Tag` | `object?` | Optional user data |

## Input Bindings

TilePanel registers these default keybindings, all rebindable via `WithInputBindings`:

| Input | Action | ActionId |
|-------|--------|----------|
| Arrow keys | Pan by 1 tile | `PanUp`, `PanDown`, `PanLeft`, `PanRight` |
| Shift+Arrow keys | Pan by 5 tiles | `PanUpFast`, `PanDownFast`, `PanLeftFast`, `PanRightFast` |
| + / = | Zoom in | `ZoomIn` |
| - | Zoom out | `ZoomOut` |
| Home | Reset to origin | `ResetPosition` |
| Mouse scroll | Zoom at cursor | `ZoomIn` / `ZoomOut` |
| Mouse drag | Pan | (drag handler) |

### Rebinding Keys

```csharp
using Hex1b.Input;
using Hex1b.Widgets;

ctx.TilePanel(dataSource, cameraX, cameraY, zoom)
    .OnPan(e => { /* ... */ })
    .OnZoom(e => { /* ... */ })
    .WithInputBindings(b =>
    {
        // Use WASD instead of arrow keys
        b.Remove(TilePanelWidget.PanUp);
        b.Remove(TilePanelWidget.PanDown);
        b.Remove(TilePanelWidget.PanLeft);
        b.Remove(TilePanelWidget.PanRight);
        b.Key(Hex1bKey.W).Triggers(TilePanelWidget.PanUp);
        b.Key(Hex1bKey.S).Triggers(TilePanelWidget.PanDown);
        b.Key(Hex1bKey.A).Triggers(TilePanelWidget.PanLeft);
        b.Key(Hex1bKey.D).Triggers(TilePanelWidget.PanRight);
    })
```

## Theming

Customize the appearance of empty tiles and POI labels:

```csharp
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(TilePanelTheme.EmptyTileForegroundColor, Hex1bColor.DarkGray)
    .Set(TilePanelTheme.EmptyTileBackgroundColor, Hex1bColor.Black)
    .Set(TilePanelTheme.EmptyTileCharacter, '.')
    .Set(TilePanelTheme.PoiLabelForegroundColor, Hex1bColor.Yellow)
    .Set(TilePanelTheme.PoiLabelBackgroundColor, Hex1bColor.FromRgb(40, 40, 40));
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `EmptyTileForegroundColor` | `Hex1bColor` | DarkGray | Foreground for tiles with no data |
| `EmptyTileBackgroundColor` | `Hex1bColor` | Default | Background for tiles with no data |
| `EmptyTileCharacter` | `char` | `·` | Character used to fill empty tiles |
| `PoiLabelForegroundColor` | `Hex1bColor` | White | POI label text color |
| `PoiLabelBackgroundColor` | `Hex1bColor` | Default | POI label background color |

## Async Loading

TilePanel loads tiles asynchronously — `GetTilesAsync` is called on a background thread, never blocking the UI. While tiles load, empty placeholders are shown. When tiles arrive, the panel automatically redraws.

::: tip Data Source Caching
If your data source involves expensive I/O (e.g., network requests), implement caching within your `ITileDataSource`. The panel calls `GetTilesAsync` whenever the viewport changes, so a fast return path keeps the UI responsive.
:::

## Extension Methods

| Method | Description |
|--------|-------------|
| `ctx.TilePanel(dataSource)` | Creates a TilePanel at origin with zoom 0 |
| `ctx.TilePanel(dataSource, cameraX, cameraY, zoomLevel)` | Creates a TilePanel with specified camera position |

## Related Widgets

- [Scroll](/guide/widgets/scroll) - For scrollable content within fixed bounds
- [Surface](/guide/widgets/surface) - For custom low-level rendering
- [Float](/guide/widgets/float) - For absolute positioning of overlays
