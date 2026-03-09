using Hex1b;
using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using SkiaSharp;

// ─────────────────────────────────────────────────────────────────────────────
// KGP Demo: Exercises KGP graphics in various widget composition scenarios.
//
// Scenarios covered:
// 1. KGP image as WindowPanel background (with text labels on top)
// 2. Draggable, resizable windows that occlude the background KGP image
// 3. KGP image inside a window (nested KGP)
// 4. Generated images (gradient, checkerboard, circle)
// 5. Real photos loaded from disk (Bonny, Bogie, Mitch, Firestarter)
// ─────────────────────────────────────────────────────────────────────────────

// Generate test images
var gradientImage = GenerateGradient(128, 64);
var checkerImage = GenerateCheckerboard(64, 64, 8);
var circleImage = GenerateCircle(64, 64);

// Load real photos from disk
var imageDir = Path.Combine(AppContext.BaseDirectory, "images");
var photoImages = new Dictionary<string, (byte[] Data, int W, int H)>();
foreach (var file in Directory.Exists(imageDir) ? Directory.GetFiles(imageDir) : [])
{
    var name = Path.GetFileNameWithoutExtension(file);
    var rgba = LoadImageFile(file);
    if (rgba != null)
        photoImages[name] = rgba.Value;
}

var windowCount = 0;
var statusMessage = "Use File menu to open windows";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics("KgpDemo", forceEnable: true)
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        options.OnRescue = args =>
        {
            File.AppendAllText("/tmp/kgp-demo-errors.log",
                $"[{DateTime.Now:HH:mm:ss.fff}] RESCUE: phase={args.Phase} error={args.Exception}\n");
        };

        return ctx =>
        {
        return ctx.VStack(outer =>
        [
            // Menu bar
            outer.MenuBar(m =>
            [
                m.Menu("File", m =>
                {
                    var items = new List<Hex1b.Widgets.IMenuChild>();
                    items.Add(m.Menu("Generated", m =>
                    [
                        m.MenuItem("Gradient").OnActivated(e =>
                            OpenImageWindow(e, "Gradient", gradientImage, 128, 64)),
                        m.MenuItem("Checkerboard").OnActivated(e =>
                            OpenImageWindow(e, "Checker", checkerImage, 64, 64)),
                        m.MenuItem("Circle").OnActivated(e =>
                            OpenImageWindow(e, "Circle", circleImage, 64, 64)),
                    ]));
                    if (photoImages.Count > 0)
                    {
                        items.Add(m.Separator());
                        foreach (var kvp in photoImages)
                        {
                            var name = Capitalize(kvp.Key);
                            var (data, w, h) = kvp.Value;
                            items.Add(m.MenuItem(name).OnActivated(e =>
                                OpenImageWindow(e, name, data, w, h)));
                        }
                    }
                    items.Add(m.Separator());
                    items.Add(m.MenuItem("Text Window").OnActivated(e => OpenTextWindow(e)));
                    items.Add(m.Separator());
                    items.Add(m.MenuItem("Quit").OnActivated(e => e.Context.RequestStop()));
                    return items;
                }),
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
                .Fill(),

            // Info bar at bottom
            outer.InfoBar([
                "Alt+Letter", "Menu",
                "Drag", "Move window",
                "Resize", "Drag edges",
                "Ctrl+C", "Exit"
            ])
        ]);
        };
    })
    .Build();

await terminal.RunAsync();

// ─────────────────────────────────────────────────────────────────────────────
// Window openers
// ─────────────────────────────────────────────────────────────────────────────

void OpenImageWindow(MenuItemActivatedEventArgs e, string name, byte[] imageData, int pixelW, int pixelH)
{
    windowCount++;
    var num = windowCount;
    var stretch = KgpImageStretch.Fit;
    var alignEnabled = false;
    var hAlignIndex = 0; // Left
    var vAlignIndex = 0; // Top
    var sizeEnabled = false;
    var maintainAspectRatio = true;
    var widthText = "20";
    var heightText = "10";

    string[] hAlignOptions = ["Left", "Center", "Right"];
    string[] vAlignOptions = ["Top", "Center", "Bottom"];
    string[] stretchOptions = ["None", "Fit", "Fill", "Stretch"];

    var window = e.Windows.Window(w =>
    {
        try
        {
            return w.VStack(v =>
            {
                // Build image widget with optional explicit cell dimensions
                var kgpImage = v.KgpImage(imageData, pixelW, pixelH,
                    v.Text($" [KGP not supported - {name} fallback]"))
                    .WithStretch(stretch);

                if (sizeEnabled
                    && int.TryParse(widthText, out var cw) && cw > 0
                    && int.TryParse(heightText, out var ch) && ch > 0)
                {
                    if (maintainAspectRatio)
                    {
                        // Compute height from width preserving pixel aspect ratio
                        // Account for terminal cell aspect (~10px wide, ~20px tall)
                        var aspectRatio = (double)pixelW / pixelH;
                        ch = Math.Max(1, (int)Math.Round(cw / aspectRatio * 0.5));
                    }
                    kgpImage = kgpImage.WithWidth(cw).WithHeight(ch);
                }

                Hex1bWidget image = kgpImage.Width(SizeHint.Fill).Height(SizeHint.Fill);

                // Wrap in Align if enabled
                if (alignEnabled)
                {
                    var hAlign = hAlignIndex switch
                    {
                        1 => Alignment.HCenter,
                        2 => Alignment.Right,
                        _ => Alignment.Left
                    };
                    var vAlign = vAlignIndex switch
                    {
                        1 => Alignment.VCenter,
                        2 => Alignment.Bottom,
                        _ => Alignment.Top
                    };
                    image = v.Align(hAlign | vAlign, image)
                        .Width(SizeHint.Fill).Height(SizeHint.Fill);
                }

                return new Hex1bWidget[]
                {
                    // Controls panel with drag handle at bottom edge (first child in VStack)
                    v.DragBarPanel(
                        v.VStack(p =>
                        [
                            p.HStack(h =>
                            [
                                h.Text(" Stretch: "),
                                h.ToggleSwitch(stretchOptions, (int)stretch)
                                    .OnSelectionChanged(ev => stretch = (KgpImageStretch)ev.SelectedIndex),
                            ]),
                            p.HStack(h =>
                            {
                                var row = new List<Hex1bWidget>
                                {
                                    h.Checkbox(alignEnabled, "Align")
                                        .OnToggled(_ => alignEnabled = !alignEnabled)
                                };
                                if (alignEnabled)
                                {
                                    row.Add(h.Text(" H:"));
                                    row.Add(h.ToggleSwitch(hAlignOptions, hAlignIndex)
                                        .OnSelectionChanged(ev => hAlignIndex = ev.SelectedIndex));
                                    row.Add(h.Text(" V:"));
                                    row.Add(h.ToggleSwitch(vAlignOptions, vAlignIndex)
                                        .OnSelectionChanged(ev => vAlignIndex = ev.SelectedIndex));
                                }
                                return row.ToArray();
                            }),
                            p.HStack(h =>
                            {
                                var row = new List<Hex1bWidget>
                                {
                                    h.Checkbox(sizeEnabled, "Size")
                                        .OnToggled(_ => sizeEnabled = !sizeEnabled)
                                };
                                if (sizeEnabled)
                                {
                                    row.Add(h.Checkbox(maintainAspectRatio, "AR")
                                        .OnToggled(_ => maintainAspectRatio = !maintainAspectRatio));
                                    row.Add(h.Text(" W:"));
                                    row.Add(h.TextBox(widthText)
                                        .OnTextChanged(ev => widthText = ev.NewText)
                                        .Width(SizeHint.Fixed(6)));
                                    if (!maintainAspectRatio)
                                    {
                                        row.Add(h.Text(" H:"));
                                        row.Add(h.TextBox(heightText)
                                            .OnTextChanged(ev => heightText = ev.NewText)
                                            .Width(SizeHint.Fixed(6)));
                                    }
                                }
                                return row.ToArray();
                            }),
                            p.HStack(h =>
                            [
                                h.Text($" {pixelW}x{pixelH}px "),
                                h.Text(" ").Width(SizeHint.Fill),
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ])
                    ).InitialSize(6).MinSize(4),

                    image,
                };
            });
        }
        catch (Exception ex)
        {
            File.AppendAllText("/tmp/kgp-demo-errors.log",
                $"[{DateTime.Now:HH:mm:ss.fff}] Window builder exception: {ex}\n");
            return w.Text($"ERROR: {ex.Message}");
        }
    })
    .Title($"{name} #{num}")
    .Size(46, 22)
    .Resizable()
    .Position(new WindowPositionSpec(WindowPosition.Center,
        OffsetX: num * 3, OffsetY: num * 2));

    e.Windows.Open(window);
    statusMessage = $"Opened {name} window #{num}";
}

void OpenTextWindow(MenuItemActivatedEventArgs e)
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
    .Resizable()
    .Position(new WindowPositionSpec(WindowPosition.Center,
        OffsetX: num * 4, OffsetY: -num));

    e.Windows.Open(window);
    statusMessage = $"Opened text window #{num}";
}

// ─────────────────────────────────────────────────────────────────────────────
// Image loading
// ─────────────────────────────────────────────────────────────────────────────

static (byte[] Data, int W, int H)? LoadImageFile(string path)
{
    try
    {
        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null) return null;

        // Ensure RGBA8888 format
        using var rgba = bitmap.ColorType == SKColorType.Rgba8888
            ? bitmap
            : bitmap.Copy(SKColorType.Rgba8888);
        if (rgba == null) return null;

        var pixels = rgba.GetPixelSpan();
        var data = new byte[pixels.Length];
        pixels.CopyTo(data);
        return (data, rgba.Width, rgba.Height);
    }
    catch
    {
        return null;
    }
}

static string Capitalize(string s) =>
    s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

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
