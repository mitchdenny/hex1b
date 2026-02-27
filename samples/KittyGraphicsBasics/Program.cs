using Hex1b;
using Hex1b.Widgets;

// Generate test images
var redGradient = GenerateGradientImage(64, 64, 255, 0, 0);
var greenGradient = GenerateGradientImage(64, 64, 0, 255, 0);
var blueGradient = GenerateGradientImage(64, 64, 0, 0, 255);
var checkerboard = GenerateCheckerboard(64, 64);
var testPattern = GenerateTestPattern(32, 32);

var windowCounter = 0;
var statusMessage = "Click 'Add Window' to create windows with KGP graphics";

// Pre-defined window configs: (title, pixelData, width, height, displayCols, displayRows)
var imageConfigs = new (string Title, byte[] Data, uint W, uint H, uint Cols, uint Rows)[]
{
    ("Red Gradient", redGradient, 64, 64, 16, 8),
    ("Green Gradient", greenGradient, 64, 64, 16, 8),
    ("Blue Gradient", blueGradient, 64, 64, 16, 8),
    ("Checkerboard", checkerboard, 64, 64, 16, 8),
    ("Test Pattern", testPattern, 32, 32, 16, 8),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithKittyGraphicsSupport()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(outer => [
            // Menu bar
            outer.HStack(h => [
                h.Button("Add Window").OnClick(e =>
                {
                    var idx = windowCounter % imageConfigs.Length;
                    var config = imageConfigs[idx];
                    windowCounter++;
                    var num = windowCounter;

                    var window = e.Windows.Window(w => w.VStack(v => [
                        v.Text($"  {config.Title} ({config.W}×{config.H}px)"),
                        v.Text(""),
                        v.KittyGraphics(config.Data, config.W, config.H)
                            .WithDisplaySize(config.Cols, config.Rows),
                        v.Text(""),
                        v.HStack(bh => [
                            bh.Text("  "),
                            bh.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                        ])
                    ]))
                    .Title($"#{num}: {config.Title}")
                    .Size((int)config.Cols + 6, (int)config.Rows + 7)
                    .Position(new WindowPositionSpec(
                        WindowPosition.Center,
                        OffsetX: (num - 1) * 3,
                        OffsetY: (num - 1) * 2))
                    .Resizable(minWidth: 12, minHeight: 8)
                    .OnClose(() => statusMessage = $"Closed window #{num}");

                    e.Windows.Open(window);
                    statusMessage = $"Opened window #{num}: {config.Title}";
                }),
                h.Text("  "),
                h.Button("Add All").OnClick(e =>
                {
                    foreach (var config in imageConfigs)
                    {
                        windowCounter++;
                        var num = windowCounter;
                        var c = config;

                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text($"  {c.Title} ({c.W}×{c.H}px)"),
                            v.Text(""),
                            v.KittyGraphics(c.Data, c.W, c.H)
                                .WithDisplaySize(c.Cols, c.Rows),
                            v.Text(""),
                            v.HStack(bh => [
                                bh.Text("  "),
                                bh.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Title($"#{num}: {c.Title}")
                        .Size((int)c.Cols + 6, (int)c.Rows + 7)
                        .Position(new WindowPositionSpec(
                            WindowPosition.Center,
                            OffsetX: (num - 1) * 3,
                            OffsetY: (num - 1) * 2))
                        .Resizable(minWidth: 12, minHeight: 8)
                        .OnClose(() => statusMessage = $"Closed window #{num}");

                        e.Windows.Open(window);
                    }
                    statusMessage = $"Opened {imageConfigs.Length} windows";
                }),
                h.Text("  "),
                h.Button("Close All").OnClick(e =>
                {
                    e.Windows.CloseAll();
                    statusMessage = "All windows closed";
                }),
                h.Text("     KGP Window Demo — drag windows by title bar")
            ]),

            outer.Separator(),

            // Window panel (MDI area)
            outer.WindowPanel().Unbounded().Fill(),

            // Status bar
            outer.InfoBar([
                "Status", statusMessage,
                "Windows", $"{windowCounter} created"
            ])
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

// ─── Image Generators ───

static byte[] GenerateGradientImage(uint w, uint h, byte r, byte g, byte b)
{
    var data = new byte[w * h * 4];
    for (uint y = 0; y < h; y++)
    {
        for (uint x = 0; x < w; x++)
        {
            var offset = (int)((y * w + x) * 4);
            var intensity = (float)(x + y) / (w + h - 2);
            data[offset] = (byte)(r * intensity);
            data[offset + 1] = (byte)(g * intensity);
            data[offset + 2] = (byte)(b * intensity);
            data[offset + 3] = 255;
        }
    }
    return data;
}

static byte[] GenerateCheckerboard(uint w, uint h)
{
    var data = new byte[w * h * 4];
    const int squareSize = 8;
    for (uint y = 0; y < h; y++)
    {
        for (uint x = 0; x < w; x++)
        {
            var offset = (int)((y * w + x) * 4);
            var isWhite = ((x / squareSize) + (y / squareSize)) % 2 == 0;
            var val = isWhite ? (byte)255 : (byte)40;
            data[offset] = val;
            data[offset + 1] = val;
            data[offset + 2] = val;
            data[offset + 3] = 255;
        }
    }
    return data;
}

static byte[] GenerateTestPattern(uint width, uint height)
{
    var data = new byte[width * height * 4];
    for (uint y = 0; y < height; y++)
    {
        for (uint x = 0; x < width; x++)
        {
            var offset = (int)((y * width + x) * 4);
            var quadrantX = x < width / 2 ? 0 : 1;
            var quadrantY = y < height / 2 ? 0 : 1;
            var quadrant = quadrantY * 2 + quadrantX;

            switch (quadrant)
            {
                case 0:
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = 0;
                    data[offset + 2] = 0;
                    break;
                case 1:
                    data[offset] = 0;
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
                case 2:
                    data[offset] = 0;
                    data[offset + 1] = 0;
                    data[offset + 2] = (byte)(x * 255 / width);
                    break;
                case 3:
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
            }
            data[offset + 3] = 255;
        }
    }
    return data;
}
