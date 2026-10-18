# Top-down helicopter

A standalone game-feasibility spike for Hex1b's experimental scene widget.
Fly a low-poly military helicopter over a small landscape, with a following
top-down perspective camera, spinning main/tail rotors, banking, and bottom-row buttons.
Mouse position controls tilt and flight; the wheel controls rotor speed and lift.
The filled scene uses colored Unicode half-blocks, not literal ASCII.

```sh
dotnet run --project samples/HelicopterDemo
```

Use a true-color terminal, at least 80 columns by 24 rows; 120 by 40 gives the
model more detail. The humanoid sample remains separate in
[`AnimatedHumanoidDemo`](../AnimatedHumanoidDemo/README.md).

## Controls

| Input | Action |
|-------|--------|
| Move mouse over the scene | Tilt and accelerate toward the pointer, relative to the scene center |
| Hold left / right mouse button | Turn left / right continuously; release to stop turning |
| Mouse wheel up / down over the scene | Increase / decrease rotor throttle and lift (0-300%) |
| WASD / arrow keys | Move north, west, south, east relative to the screen |
| Q / E | Turn the helicopter left / right |
| Space | Level and brake horizontal motion; throttle is unchanged |
| P | Pause / resume |
| R | Reset to the helipad |
| Z / X | Zoom in / out |
| Tab / Shift+Tab, Enter | Focus and activate bottom-row buttons |
| Ctrl+C / Quit button | Exit |

Move the cursor farther from the scene center for more tilt and acceleration.
A small center dead zone levels the helicopter and lets drag slow it down.
Tilt stays screen-relative even while you turn. Moving into the toolbar releases
tilt/rotation; the toolbar keeps its normal mouse clicks and wheel behavior.
Use a terminal with all-motion mouse reporting (enabled by the sample).

Brake, reset, and pause clear active steering. After braking/resetting or resuming,
move the mouse again to engage tilt; leaving a stationary cursor off-center does
not immediately restart flight. Pausing ignores mouse flight/rotor inputs.
Reset also restores the default rotor speed.

## Altitude and throttle

The sample starts hovering at altitude 2.4 scene units. **100% rotor speed holds a
level hover**; above that climbs, below that descends. Vertical momentum and drag
make the response gradual rather than snapping to a target height.

**More tilt means less upward lift.** Banking at 100% loses altitude; add
throttle to compensate. The lift approximation uses squared rotor speed
multiplied by the rotor disc's upward orientation. This is an arcade throttle
model, not a realistic collective-pitch/rotor simulation.

The header shows altitude, vertical speed (`V/S`), throttle, and heading.
Perspective makes the helicopter appear larger as it rises toward the overhead
camera; its ground shadow also shifts and grows with height. Zoom remains a
separate view control and does not affect flight.

Altitude is bounded between the ground (0) and a ceiling (14). Ground contact
stops vertical/horizontal movement and levels the model without a crash mechanic.
Increase throttle above hover to take off again. Stopped rotors let the chopper
fall to the ground. Pause freezes altitude and vertical momentum; reset restores
the initial hover. Brake does not stop a climb/descent or change rotor throttle.

## Scope

The keyboard controls remain available as a fallback. Hold movement keys to add
impulses using the terminal's key-repeat events; the initial repeat delay depends
on your terminal/OS. Drag slows the helicopter after input stops.

This is a flight toy, not a flight/combat simulator: the wing/pod
additions are decorative, buildings have no collision, the map has bounded
movement, and the ground shadow is a simple ellipse rather than a shadow map.

## Model and Native AOT

The base model is **[Helicopter by Zsky](https://poly.pizza/m/hG2Qr0A3zR)**,
licensed **CC BY 3.0**. It is a military transport-style helicopter adapted into
a stylized gunship with simple stub wings/pods, **not an Apache replica**.
See [`Assets/NOTICE.md`](Assets/NOTICE.md) for attribution and modifications.
The source page advertises 1,056 triangles; the actual GLB contains 1,088,
including 48 main-rotor and 36 tail-rotor triangles. All are preserved.

The bundled, roughly 39 KB mesh is converted ahead of time. The sample loads
only flat colored triangles and rotor pivots with `BinaryReader`, without a
runtime glTF parser, reflection, textures, or skinning. Rotor motion is
procedural; the original asset has no animation tracks.

`PublishAot` is enabled in this sample project. For example, on Apple Silicon:

```sh
dotnet publish samples/HelicopterDemo -c Release -r osx-arm64
samples/HelicopterDemo/bin/Release/net10.0/osx-arm64/publish/HelicopterDemo
```

That native publish/run was verified on macOS ARM64 without trimming/AOT
warnings. Other platforms need the corresponding .NET Native AOT prerequisites
and runtime identifier; they have not been native-publish-tested for this spike.

### Reproduce the converted asset

The generator is specific to the pinned source, verifies its SHA-256, bakes its
orientation/scale, and separates rotor triangles by their material and position.
It requires only Python 3's standard library. No conversion or download happens
during a normal build.

From the repository root:

```sh
curl --fail --location \
  https://static.poly.pizza/6f3cef79-867c-4b3c-9d97-346e465e4079.glb \
  --output /tmp/hex1b-helicopter.glb
python3 samples/HelicopterDemo/Assets/prepare-model.py /tmp/hex1b-helicopter.glb
```

To run the deterministic flight/model tests and headless UI checks:

```sh
dotnet test --project tests/HelicopterDemo.Tests -v q --no-progress
```
