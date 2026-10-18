# Animated humanoid scene spike

Render a pre-existing animated humanoid in Hex1b's experimental scene widget:

```sh
dotnet run --project samples/AnimatedHumanoidDemo
```

Use a true-color terminal, ideally at least 100 columns by 36 rows (80 by 24 also
works). The model is bundled: no network access or asset conversion is needed at
runtime.

The bottom buttons select **Idle, Walk, Run, Dance, Wave, and Jump**. Click them
or use Tab / Shift+Tab and Enter. The number keys 1-6 select the same clips.
Idle, Walk, Run, and Dance loop; Wave and Jump play once and hold their final pose.
Selecting any animation restarts it and resumes playback.

| Control | Effect |
|---------|--------|
| Space / Pause button | Pause or resume |
| Restart button | Replay the current clip from the start |
| W / Wireframe button | Switch solid half-block / Braille wireframe rendering |
| Left / Right | Orbit the camera |
| Up / Down | Zoom in / out |
| Quit button / Ctrl+C | Exit |

## What this explores

The bundled CC0 **Robot Expressive** has 7,214 vertices and 14 authored animation
clips. SharpGLTF decodes 3,234 triangles (omitting three degenerate source
triangles). See [asset attribution and provenance](Assets/NOTICE.md).
Six clips are exposed to keep the UI small.

SharpGLTF Runtime loads the GLB and evaluates its hierarchy, skinning, and morph
targets on the CPU. The sample copies the resulting positions into persistent
`SceneBufferGeometry` vertex arrays; the existing `SceneWidget` handles projection,
lighting, depth testing, and terminal output. Each glTF primitive keeps its base
material color. Rest-pose bounds normalize the character to three scene units
high without resizing it during animations.

The upstream FBX conversion contains non-unit joint weights. The bundled copy
normalizes those weights once using the reproducible script in `Assets/`.
Runtime loading keeps SharpGLTF's strict validation enabled.

All loading and animation code lives in this sample. There are **no new Hex1b
APIs or library dependencies**. This is not a general-purpose glTF importer:
textures, PBR shading, animation blending, root-motion extraction, and arbitrary
asset selection are outside the spike. The scene renderer uses flat face normals.
Output uses Unicode half-blocks or Braille, not literal 7-bit ASCII.

The header reports triangle count, CPU pose-update time, and time between widget
builds. Redraws are requested every 33 ms; this is not a guaranteed frame rate.
The frame interval includes scheduling/rendering/output overhead, while the pose
timer excludes rasterization. These are exploratory indicators, not a benchmark
or a claim about how many characters a game can support.

## Checks

```sh
dotnet test --project tests/AnimatedHumanoidDemo.Tests
```

The checks load the real bundled asset, verify deformation and playback semantics,
and drive the actual scene widget and buttons in headless 80x24 and 120x40 terminals.
