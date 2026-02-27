using Hex1b;
using Hex1b.Widgets;

// ─── Pre-alt-screen KGP source rectangle clipping test ───
// Draw a 4-quadrant image: red(TL), green(TR), blue(BL), yellow(BR)
// Then use source rects to show each corner separately
{
    // 8x8 RGBA image with 4 colored quadrants
    var pixels = new byte[8 * 8 * 4];
    for (int y = 0; y < 8; y++)
    {
        for (int x = 0; x < 8; x++)
        {
            var i = (y * 8 + x) * 4;
            if (x < 4 && y < 4)      { pixels[i] = 255; pixels[i+1] = 0;   pixels[i+2] = 0;   } // TL = red
            else if (x >= 4 && y < 4) { pixels[i] = 0;   pixels[i+1] = 255; pixels[i+2] = 0;   } // TR = green
            else if (x < 4 && y >= 4) { pixels[i] = 0;   pixels[i+1] = 0;   pixels[i+2] = 255; } // BL = blue
            else                      { pixels[i] = 255; pixels[i+1] = 255; pixels[i+2] = 0;   } // BR = yellow
            pixels[i+3] = 255;
        }
    }
    var b64 = Convert.ToBase64String(pixels);

    Console.WriteLine("KGP Source Rectangle Clipping Test");
    Console.WriteLine("==================================");
    Console.WriteLine("Image has 4 quadrants: Red(TL) Green(TR) Blue(BL) Yellow(BR)");
    Console.WriteLine();

    // Transmit the image (a=t, transmit only — no display)
    Console.Write($"\x1b_Ga=t,f=32,s=8,v=8,i=90,q=2;{b64}\x1b\\");

    // Place 1: Full image (all 4 colors visible)
    Console.Write("Full:    ");
    Console.Write("\x1b_Ga=p,i=90,c=8,r=4,q=2\x1b\\");
    Console.WriteLine();
    Console.WriteLine();

    // Place 2: Top-left corner (x=0, y=0, w=4, h=4) — should be RED only
    Console.Write("Top-L:   ");
    Console.Write("\x1b_Ga=p,i=90,x=0,y=0,w=4,h=4,c=4,r=2,q=2\x1b\\");
    Console.Write("  <- should be RED");
    Console.WriteLine();
    Console.WriteLine();

    // Place 3: Top-right corner (x=4, y=0, w=4, h=4) — should be GREEN only
    Console.Write("Top-R:   ");
    Console.Write("\x1b_Ga=p,i=90,x=4,y=0,w=4,h=4,c=4,r=2,q=2\x1b\\");
    Console.Write("  <- should be GREEN");
    Console.WriteLine();
    Console.WriteLine();

    // Place 4: Bottom-left corner (x=0, y=4, w=4, h=4) — should be BLUE only
    Console.Write("Bot-L:   ");
    Console.Write("\x1b_Ga=p,i=90,x=0,y=4,w=4,h=4,c=4,r=2,q=2\x1b\\");
    Console.Write("  <- should be BLUE");
    Console.WriteLine();
    Console.WriteLine();

    // Place 5: Bottom-right corner (x=4, y=4, w=4, h=4) — should be YELLOW only
    Console.Write("Bot-R:   ");
    Console.Write("\x1b_Ga=p,i=90,x=4,y=4,w=4,h=4,c=4,r=2,q=2\x1b\\");
    Console.Write("  <- should be YELLOW");
    Console.WriteLine();
    Console.WriteLine();

    Console.WriteLine("If each shows a different single color, source rect clipping works!");
    Console.WriteLine("If they all show 4 colors, the terminal ignores x,y,w,h.");
    Console.WriteLine();
    Console.Write("Press Enter to continue to the app...");
    Console.ReadLine();

    // Delete test images before entering alt screen
    Console.Write("\x1b_Ga=d,d=A,q=2\x1b\\");
}

// ─── Main app ───

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
            outer.MenuBar(m => [
                m.Menu("Windows", m => [
                    m.MenuItem("Add Window").OnActivated(e =>
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
                    m.MenuItem("Add All").OnActivated(e =>
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
                    m.MenuItem("Close All").OnActivated(e =>
                    {
                        e.Windows.CloseAll();
                        statusMessage = "All windows closed";
                    }),
                    m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
                ])
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
