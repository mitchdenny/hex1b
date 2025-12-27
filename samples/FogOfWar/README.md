# Fog of War Demo

This sample demonstrates the `FogOfWarPresentationAdapter`, which applies a visual effect that darkens colors based on distance from the mouse cursor position.

## What is the Fog of War Filter?

The fog of war filter is a presentation adapter decorator that intercepts ANSI output sequences and modifies colors so that:
- Areas near the mouse cursor are displayed with full color
- Areas farther from the mouse cursor gradually darken toward black
- The effect creates a "spotlight" appearance where only the area around the mouse is fully visible

## How It Works

1. The filter wraps an existing `IHex1bTerminalPresentationAdapter`
2. It tracks mouse position by parsing SGR mouse input sequences
3. When output is sent to the presentation layer, it:
   - Parses ANSI escape sequences to find color codes
   - Calculates the distance from each cell to the mouse cursor
   - Applies a fog factor based on distance (0 = completely dark, 1 = full color)
   - Modifies RGB color values by multiplying by the fog factor
   - Re-encodes the modified ANSI sequences

## Running the Demo

```bash
dotnet run --project samples/FogOfWar
```

Move your mouse around the terminal to see the fog effect in action!

## Usage in Your Application

```csharp
// Create your base presentation adapter
var basePresentation = new ConsolePresentationAdapter(enableMouse: true);

// Wrap it with the fog of war adapter
var fogPresentation = new FogOfWarPresentationAdapter(
    basePresentation, 
    maxDistance: 15.0  // Distance at which colors become completely black
);

// Use the fog presentation adapter when creating the terminal
using var terminal = new Hex1bTerminal(fogPresentation, workload);

// Create your app with mouse support enabled
await using var app = new Hex1bApp(
    builder,
    new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        EnableMouse = true  // Required for fog of war effect
    }
);
```

## Configuration

The `FogOfWarPresentationAdapter` constructor accepts:
- `inner`: The underlying presentation adapter to wrap
- `maxDistance`: The maximum distance (in cells) at which colors are completely black (default: 20.0)

Smaller values create a tighter spotlight effect, while larger values create a more gradual fade.
