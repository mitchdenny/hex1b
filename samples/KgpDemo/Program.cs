using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;

// ─────────────────────────────────────────────────────────────────────────────
// KGP Demo: Exercises KGP graphics in various widget composition scenarios.
//
// Scenarios covered:
// 1. KGP image as WindowPanel background (with text labels on top)
// 2. Draggable windows that occlude the background KGP image
// 3. KGP image inside a window (nested KGP)
// 4. Multiple KGP images at different z-orders
// 5. Surface layer with KGP cells (low-level compositing)
// ─────────────────────────────────────────────────────────────────────────────

// Generate test images
var gradientImage = GenerateGradient(128, 64);
var checkerImage = GenerateCheckerboard(64, 64, 8);
var circleImage = GenerateCircle(64, 64);

var windowCount = 0;
var statusMessage = "Press menu buttons to open windows";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics("KgpDemo", forceEnable: true)
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(outer =>
        [
            // Menu bar
            outer.HStack(menu =>
            [
                menu.Button(" Gradient Window ").OnClick(e =>
                {
                    windowCount++;
                    var num = windowCount;
                    var window = e.Windows.Window(w => w.VStack(v =>
                    [
                        v.Text(" KGP gradient inside a window:"),
                        v.Text(""),
                        v.KgpImage(gradientImage, 128, 64,
                            v.Text(" [KGP not supported - gradient fallback]"),
                            width: 30, height: 10),
                        v.Text(""),
                        v.HStack(h =>
                        [
                            h.Text(" "),
                            h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                        ])
                    ]))
                    .Title($"Gradient #{num}")
                    .Size(34, 16)
                    .Position(new WindowPositionSpec(WindowPosition.Center,
                        OffsetX: num * 3, OffsetY: num * 2));

                    e.Windows.Open(window);
                    statusMessage = $"Opened gradient window #{num}";
                }),

                menu.Button(" Checkerboard Window ").OnClick(e =>
                {
                    windowCount++;
                    var num = windowCount;
                    var window = e.Windows.Window(w => w.VStack(v =>
                    [
                        v.Text(" KGP checkerboard:"),
                        v.Text(""),
                        v.KgpImage(checkerImage, 64, 64,
                            v.Text(" [KGP not supported - checker fallback]"),
                            width: 20, height: 10),
                        v.Text(""),
                        v.HStack(h =>
                        [
                            h.Text(" "),
                            h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                        ])
                    ]))
                    .Title($"Checker #{num}")
                    .Size(24, 16)
                    .Position(new WindowPositionSpec(WindowPosition.Center,
                        OffsetX: -num * 3, OffsetY: num * 2));

                    e.Windows.Open(window);
                    statusMessage = $"Opened checker window #{num}";
                }),

                menu.Button(" Text Window ").OnClick(e =>
                {
                    windowCount++;
                    var num = windowCount;
                    var window = e.Windows.Window(w => w.VStack(v =>
                    [
                        v.Text(""),
                        v.Text("  This is a plain text window."),
                        v.Text("  Drag it over the background"),
                        v.Text("  KGP image to test occlusion."),
                        v.Text(""),
                        v.Text($"  Window #{num}"),
                        v.Text(""),
                        v.HStack(h =>
                        [
                            h.Text(" "),
                            h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                        ])
                    ]))
                    .Title($"Text #{num}")
                    .Size(36, 11)
                    .Position(new WindowPositionSpec(WindowPosition.Center,
                        OffsetX: num * 4, OffsetY: -num));

                    e.Windows.Open(window);
                    statusMessage = $"Opened text window #{num}";
                }),

                menu.Text($"  │ {statusMessage}")
            ]),

            // WindowPanel with KGP background image
            outer.WindowPanel()
                .Background(bg =>
                    bg.KgpImage(circleImage, 64, 64,
                        bg.Surface(s =>
                        [
                            s.Layer(surf => DrawAsciiArtFallback(surf))
                        ]).Width(SizeHint.Fill).Height(SizeHint.Fill),
                        width: 40, height: 20)
                )
                .Fill()
        ]);
    })
    .Build();

await terminal.RunAsync();

// ─────────────────────────────────────────────────────────────────────────────
// Image generators
// ─────────────────────────────────────────────────────────────────────────────

static byte[] GenerateGradient(int w, int h)
{
    var data = new byte[w * h * 4];
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var idx = (y * w + x) * 4;
            data[idx] = (byte)(x * 255 / w);       // R
            data[idx + 1] = (byte)(y * 255 / h);   // G
            data[idx + 2] = (byte)(128 + 127 * Math.Sin(x * 0.1 + y * 0.05)); // B
            data[idx + 3] = 255;                    // A
        }
    }
    return data;
}

static byte[] GenerateCheckerboard(int w, int h, int squareSize)
{
    var data = new byte[w * h * 4];
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var idx = (y * w + x) * 4;
            var isWhite = ((x / squareSize) + (y / squareSize)) % 2 == 0;
            var c = isWhite ? (byte)220 : (byte)40;
            data[idx] = c;
            data[idx + 1] = isWhite ? (byte)180 : (byte)80;
            data[idx + 2] = isWhite ? (byte)255 : (byte)160;
            data[idx + 3] = 255;
        }
    }
    return data;
}

static byte[] GenerateCircle(int w, int h)
{
    var data = new byte[w * h * 4];
    var cx = w / 2.0;
    var cy = h / 2.0;
    var r = Math.Min(w, h) / 2.0 - 2;

    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var idx = (y * w + x) * 4;
            var dx = x - cx;
            var dy = y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist <= r)
            {
                var angle = Math.Atan2(dy, dx);
                var t = dist / r;
                data[idx] = (byte)(128 + 127 * Math.Cos(angle));           // R
                data[idx + 1] = (byte)(128 + 127 * Math.Sin(angle * 2));   // G
                data[idx + 2] = (byte)(255 * (1 - t));                     // B
                data[idx + 3] = 255;
            }
            else
            {
                data[idx] = 30;
                data[idx + 1] = 30;
                data[idx + 2] = 50;
                data[idx + 3] = 255;
            }
        }
    }
    return data;
}

static void DrawAsciiArtFallback(Surface surface)
{
    var msg = new[]
    {
        "╔══════════════════════════════════════╗",
        "║   KGP Not Supported - ASCII Art     ║",
        "║                                      ║",
        "║       ████████████████████           ║",
        "║     ██                    ██         ║",
        "║   ██    ██          ██      ██       ║",
        "║   ██                        ██       ║",
        "║   ██    ██              ██  ██       ║",
        "║     ██    ████████████    ██         ║",
        "║       ████████████████████           ║",
        "║                                      ║",
        "║  Drag windows over this background   ║",
        "║  to test occlusion behavior.         ║",
        "╚══════════════════════════════════════╝"
    };

    for (var y = 0; y < Math.Min(msg.Length, surface.Height); y++)
    {
        for (var x = 0; x < Math.Min(msg[y].Length, surface.Width); x++)
        {
            surface[x, y] = new SurfaceCell(
                msg[y][x].ToString(),
                Hex1bColor.FromRgb(100, 180, 255),
                Hex1bColor.FromRgb(20, 20, 40));
        }
    }
}
