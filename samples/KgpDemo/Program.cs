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
// 6. xterm.js-safe static previews with bounded payloads and a single placement
// 7. Animated KGP images that begin playing when their window opens
// ─────────────────────────────────────────────────────────────────────────────

const int xtermSafeMaxPhotoWidth = 640;
const int xtermSafeMaxPhotoHeight = 480;
const int xtermSafeMaxRawBytes = 2 * 1024 * 1024;

// Generate test images
var gradientImage = GenerateGradient(128, 64);
var checkerImage = GenerateCheckerboard(64, 64, 8);
var circleImage = GenerateCircle(64, 64);

var xtermSafeImages = new List<(string Name, byte[] Data, int W, int H)>();
AddXtermSafeImage(
    xtermSafeImages, "Gradient", GenerateGradient(320, 180), 320, 180, xtermSafeMaxRawBytes);
AddXtermSafeImage(
    xtermSafeImages, "Checkerboard", GenerateCheckerboard(256, 256, 16), 256, 256, xtermSafeMaxRawBytes);
AddXtermSafeImage(
    xtermSafeImages, "Circle", GenerateCircle(256, 256), 256, 256, xtermSafeMaxRawBytes);

// Load real photos from disk
var imageDir = Path.Combine(AppContext.BaseDirectory, "images");
var catFightAnimation = LoadAnimationFile(Path.Combine(imageDir, "cat-fight.gif"));
var photoImages = new Dictionary<string, (byte[] Data, int W, int H)>();
foreach (var file in Directory.Exists(imageDir) ? Directory.GetFiles(imageDir) : [])
{
    if (Path.GetExtension(file).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        continue;

    var name = Path.GetFileNameWithoutExtension(file);
    var rgba = LoadImageFile(file);
    if (rgba != null)
        photoImages[name] = rgba.Value;

    var xtermSafeRgba = LoadImageFile(file, xtermSafeMaxPhotoWidth, xtermSafeMaxPhotoHeight);
    if (xtermSafeRgba != null)
    {
        var safe = xtermSafeRgba.Value;
        AddXtermSafeImage(
            xtermSafeImages, Capitalize(name), safe.Data, safe.W, safe.H, xtermSafeMaxRawBytes);
    }
}

var windowCount = 0;
var statusMessage = "Use File menu to open windows";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics("KgpDemo", forceEnable: true)
    .WithMouse()
    .WithHex1bApp(
        options =>
        {
            options.FrameRateLimitMs = 33;
            options.OnRescue = args =>
            {
                File.AppendAllText("/tmp/kgp-demo-errors.log",
                    $"[{DateTime.Now:HH:mm:ss.fff}] RESCUE: phase={args.Phase} error={args.Exception}\n");
            };
        },
        ctx =>
        {
        return ctx.VStack(outer =>
        [
            // Menu bar
            outer.MenuBar(m =>
            [
                m.Menu("File", m =>
                {
                    var items = new List<Hex1b.Widgets.IMenuChild>();
                    items.Add(m.Menu("xterm.js Safe", safeMenu =>
                    {
                        var safeItems = new List<Hex1b.Widgets.IMenuChild>();
                        foreach (var (name, data, w, h) in xtermSafeImages)
                        {
                            safeItems.Add(safeMenu.MenuItem($"{name} ({w}x{h})").OnActivated(e =>
                                OpenXtermSafeImageWindow(e, name, data, w, h)));
                        }
                        return safeItems;
                    }));
                    items.Add(m.Separator());
                    items.Add(m.Menu("Generated", m =>
                    [
                        m.MenuItem("Gradient").OnActivated(e =>
                            OpenImageWindow(e, "Gradient", gradientImage, 128, 64)),
                        m.MenuItem("Checkerboard").OnActivated(e =>
                            OpenImageWindow(e, "Checker", checkerImage, 64, 64)),
                        m.MenuItem("Circle").OnActivated(e =>
                            OpenImageWindow(e, "Circle", circleImage, 64, 64)),
                    ]));
                    items.Add(m.Menu("Animated", m =>
                    [
                        m.MenuItem("Cat Fight").OnActivated(e =>
                            OpenAnimationWindow(
                                e,
                                "Cat Fight",
                                catFightAnimation.Frames,
                                catFightAnimation.W,
                                catFightAnimation.H)),
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
                    items.Add(m.MenuItem("Large Text Window").OnActivated(e => OpenTextWindow(e, large: true)));
                    items.Add(m.MenuItem("Bare KGP Window").OnActivated(e => OpenBareImageWindow(e, "Circle", circleImage, 64, 64)));
                    items.Add(m.Separator());
                    items.Add(m.MenuItem("Quit").OnActivated(e => e.Context.RequestStop()));
                    return items;
                }),
            ]),

            // WindowPanel without background graphics for resize debugging
            outer.WindowPanel()
                .Fill(),

            // Info bar at bottom
            outer.InfoBar([
                "Alt+Letter", "Menu",
                "Drag", "Move window",
                "Resize", "Drag edges",
                "Ctrl+C", "Exit"
            ])
        ]);
    })
    .Build();

await terminal.RunAsync();

// ─────────────────────────────────────────────────────────────────────────────
// Window openers
// ─────────────────────────────────────────────────────────────────────────────

void OpenXtermSafeImageWindow(
    MenuItemActivatedEventArgs e,
    string name,
    byte[] imageData,
    int pixelW,
    int pixelH)
{
    var window = e.Windows.Window(w => w.VStack(v =>
        [
            v.KgpImage(imageData, pixelW, pixelH,
                    img => img.Text($" [KGP not supported - {name} fallback]"))
                .Stretch(KgpImageStretch.Fit)
                .AboveText()
                .Width(SizeHint.Fill)
                .Height(SizeHint.Fill),
            v.Text($" Drag title or edges - {pixelW}x{pixelH} RGBA - Esc closes")
        ]))
        .Title($"xterm.js Safe - {name}")
        .Size(46, 22)
        .Resizable()
        .Position(WindowPositionSpec.Center);

    e.Windows.Open(window);
    statusMessage = $"Opened xterm.js-safe {name} preview";
}

void OpenAnimationWindow(
    MenuItemActivatedEventArgs e,
    string name,
    IReadOnlyList<KgpAnimationFrame> frames,
    int pixelW,
    int pixelH)
{
    windowCount++;
    var num = windowCount;

    var window = e.Windows.Window(w =>
        w.KgpAnimation(
                frames,
                pixelW,
                pixelH,
                img => img.Text($" [KGP not supported - {name} fallback]"))
            .Playing()
            .Fit()
            .AboveText()
            .Width(SizeHint.Fill)
            .Height(SizeHint.Fill))
        .Title($"{name} #{num}")
        .Size(46, 22)
        .Resizable()
        .Position(new WindowPositionSpec(
            WindowPosition.Center,
            OffsetX: num * 3,
            OffsetY: num * 2));

    e.Windows.Open(window);
    statusMessage = $"Opened playing {name} animation #{num}";
}

void OpenImageWindow(MenuItemActivatedEventArgs e, string name, byte[] imageData, int pixelW, int pixelH)
{
    windowCount++;
    var num = windowCount;
    var stretch = KgpImageStretch.Fit;
    var renderKgp = true;
    var renderTextBody = false;
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
                Hex1bWidget image;
                if (renderTextBody)
                {
                    image = v.VStack(msg =>
                    [
                        msg.Text($" {name} diagnostic text mode"),
                        msg.Text(" This keeps the same large window"),
                        msg.Text(" and control panel, but removes"),
                        msg.Text(" the KGP/placeholder body subtree."),
                        msg.Text(""),
                        msg.Text(" Move the window, resize the terminal,"),
                        msg.Text(" and compare this with placeholder"),
                        msg.Text(" and KGP rendering modes.")
                    ]).Width(SizeHint.Fill).Height(SizeHint.Fill);
                }
                else if (renderKgp)
                {
                    // Build image widget with optional explicit cell dimensions
                    var kgpImage = v.KgpImage(imageData, pixelW, pixelH,
                        img => img.Text($" [KGP not supported - {name} fallback]"))
                        .Stretch(stretch);

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
                        kgpImage = kgpImage.Width(cw).Height(ch);
                    }

                    image = kgpImage.Width(SizeHint.Fill).Height(SizeHint.Fill);

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
                }
                else
                {
                    image = v.Align(Alignment.Center,
                            v.VStack(msg =>
                            [
                                msg.Text("KGP rendering disabled"),
                                msg.Text("Resize the terminal, then"),
                                msg.Text("re-enable to compare behavior.")
                            ]))
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
                                h.Checkbox(renderKgp, "Render KGP")
                                    .OnToggled(_ => renderKgp = !renderKgp),
                                h.Checkbox(renderTextBody, "Text body")
                                    .OnToggled(_ => renderTextBody = !renderTextBody),
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

void OpenTextWindow(MenuItemActivatedEventArgs e, bool large = false)
{
    windowCount++;
    var num = windowCount;
    var title = large ? $"Large Text #{num}" : $"Text #{num}";
    var size = large ? (Width: 46, Height: 22) : (Width: 36, Height: 11);
    var offsetX = large ? num * 3 : num * 4;
    var offsetY = large ? num * 2 : -num;

    var bodyLines = large
        ? new[]
        {
            "",
            "  Diagnostic large plain text window.",
            "  Same outer size as the image window,",
            "  but without DragBarPanel, toggles,",
            "  text boxes, alignment, or KGP body.",
            "",
            "  If this survives move+resize while the",
            "  image window still disappears, the bug",
            "  is likely inside the image window shell",
            "  or its body subtree rather than size alone.",
            "",
            $"  Window #{num}",
            "",
        }
        : new[]
        {
            "",
            "  This is a plain text window.",
            "  Drag it over the background",
            "  KGP image to test occlusion.",
            "",
            $"  Window #{num}",
            "",
        };

    var window = e.Windows.Window(w => w.VStack(v =>
    {
        var widgets = new List<Hex1bWidget>(bodyLines.Length + 1);
        foreach (var line in bodyLines)
        {
            widgets.Add(v.Text(line));
        }

        widgets.Add(v.HStack(h =>
        [
            h.Text(" "),
            h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
        ]));

        return widgets.ToArray();
    }))
    .Title(title)
    .Size(size.Width, size.Height)
    .Resizable()
    .Position(new WindowPositionSpec(WindowPosition.Center,
        OffsetX: offsetX, OffsetY: offsetY));

    e.Windows.Open(window);
    statusMessage = large
        ? $"Opened large text window #{num}"
        : $"Opened text window #{num}";
}

void OpenBareImageWindow(MenuItemActivatedEventArgs e, string name, byte[] imageData, int pixelW, int pixelH)
{
    windowCount++;
    var num = windowCount;

    var window = e.Windows.Window(w =>
        w.KgpImage(imageData, pixelW, pixelH,
                img => img.Text($" [KGP not supported - {name} fallback]"))
            .Stretch(KgpImageStretch.Fit)
            .Width(SizeHint.Fill)
            .Height(SizeHint.Fill))
        .Title($"Bare {name} #{num}")
        .Size(24, 12)
        .Resizable()
        .Position(new WindowPositionSpec(WindowPosition.Center,
            OffsetX: num * 2, OffsetY: num));

    e.Windows.Open(window);
    statusMessage = $"Opened bare {name} window #{num}";
}

// ─────────────────────────────────────────────────────────────────────────────
// Image loading
// ─────────────────────────────────────────────────────────────────────────────

static (IReadOnlyList<KgpAnimationFrame> Frames, int W, int H) LoadAnimationFile(string path)
{
    var encoded = File.ReadAllBytes(path);
    var preview = GifDecoder.DecodeFirstFrame(encoded)
        ?? throw new InvalidDataException($"Could not decode animated GIF: {path}");
    var frames = GifDecoder.DecodeAnimation(encoded, preview.Width, preview.Height)
        ?? throw new InvalidDataException($"Could not decode animated GIF frames: {path}");
    return (frames, preview.Width, preview.Height);
}

static (byte[] Data, int W, int H)? LoadImageFile(
    string path,
    int? maxWidth = null,
    int? maxHeight = null)
{
    try
    {
        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null) return null;

        SKBitmap? scaled = null;
        try
        {
            if (maxWidth is > 0 && maxHeight is > 0
                && (bitmap.Width > maxWidth || bitmap.Height > maxHeight))
            {
                var scale = Math.Min((double)maxWidth.Value / bitmap.Width, (double)maxHeight.Value / bitmap.Height);
                var scaledWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                var scaledHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
                scaled = bitmap.Resize(
                    new SKImageInfo(scaledWidth, scaledHeight),
                    SKSamplingOptions.Default);
                if (scaled == null) return null;
            }

            using var rgba = (scaled ?? bitmap).Copy(SKColorType.Rgba8888);
            if (rgba == null) return null;

            var pixels = rgba.GetPixelSpan();
            var data = new byte[pixels.Length];
            pixels.CopyTo(data);
            return (data, rgba.Width, rgba.Height);
        }
        finally
        {
            scaled?.Dispose();
        }
    }
    catch
    {
        return null;
    }
}

static void AddXtermSafeImage(
    List<(string Name, byte[] Data, int W, int H)> images,
    string name,
    byte[] data,
    int width,
    int height,
    int maxRawBytes)
{
    if (data.Length != checked(width * height * 4))
        throw new ArgumentException("Image data must contain RGBA32 pixels.", nameof(data));
    if (data.Length > maxRawBytes)
        throw new ArgumentException(
            $"Image data must not exceed {maxRawBytes} bytes for the xterm.js-safe set.",
            nameof(data));

    images.Add((name, data, width, height));
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
