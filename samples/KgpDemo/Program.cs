// KgpDemo - Interactive demonstration of KGP (Kitty Graphics Protocol) support in Hex1b.
//
// Navigate between scenes using the picker at the bottom.
// Each scene demonstrates a different aspect of KGP support.

using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

var scenes = new[] { "Image Display", "Z-Ordering", "Surface Layers", "Multiple Images", "Computed Effects" };
var selectedScene = 0;

// Pre-generate test images
var gradientImage = GenerateGradientImage(80, 40);
var redImage = GenerateSolidImage(40, 40, 220, 60, 60);
var greenImage = GenerateSolidImage(40, 40, 60, 200, 60);
var blueImage = GenerateSolidImage(40, 40, 60, 80, 220);
var checkerImage = GenerateCheckerImage(80, 40);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text($" ◆ KGP Demo — {scenes[selectedScene]}")),
            v.Text(" Tab to navigate, Enter to interact, Ctrl+C to exit"),
            v.Separator(),

            BuildScene(v, selectedScene),

            v.Separator(),
            v.Center(vc =>
                vc.HStack(h => [
                    h.Text("Scene: "),
                    h.Picker(scenes, selectedScene)
                        .OnSelectionChanged(e => selectedScene = e.SelectedIndex)
                ]).Height(SizeHint.Content)
            ).Height(SizeHint.Content)
        ]);
    })
    .Build();

await terminal.RunAsync();

// --- Scene builders ---

Hex1bWidget BuildScene<TParent>(WidgetContext<TParent> ctx, int scene) where TParent : Hex1bWidget
{
    return scene switch
    {
        0 => BuildImageDisplayScene(ctx),
        1 => BuildZOrderScene(ctx),
        2 => BuildSurfaceLayerScene(ctx),
        3 => BuildMultiImageScene(ctx),
        4 => BuildComputedScene(ctx),
        _ => ctx.Text("Unknown scene"),
    };
}

Hex1bWidget BuildImageDisplayScene<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text(" KGP images render in terminals that support the Kitty Graphics Protocol."),
        v.Text(" Other terminals see the fallback text instead."),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Border(
                h.KgpImage(gradientImage, 80, 40, "[gradient: 80x40 RGBA]", width: 20, height: 5)
            ).Title("Gradient"),
            h.Text("  "),
            h.Border(
                h.KgpImage(checkerImage, 80, 40, "[checker: 80x40 RGBA]", width: 20, height: 5)
            ).Title("Checkerboard"),
        ]).Height(SizeHint.Content),
        v.Text(""),
        v.Text(" The KgpImageWidget sends RGBA32 pixel data to the terminal via APC sequences."),
        v.Text(" Image data is base64-encoded and transmitted once, then placed with a=p."),
    ]);
}

Hex1bWidget BuildZOrderScene<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text(" KGP supports z-ordering: images can render below or above text."),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Border(
                h.KgpImage(gradientImage, 80, 40, "[below text]", width: 20, height: 5)
                    .BelowText()
            ).Title("z < 0 (Below Text)"),
            h.Text("  "),
            h.Border(
                h.KgpImage(gradientImage, 80, 40, "[above text]", width: 20, height: 5)
                    .AboveText()
            ).Title("z > 0 (Above Text)"),
        ]).Height(SizeHint.Content),
        v.Text(""),
        v.Text(" Below-text: image renders behind text characters (good for backgrounds)"),
        v.Text(" Above-text: image renders on top of text (good for overlays)"),
    ]);
}

Hex1bWidget BuildSurfaceLayerScene<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text(" KGP integrates with the Surface layer system for compositing."),
        v.Text(" Draw layers can contain KGP data alongside text layers."),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Border(
                h.Surface(s =>
                {
                    var layers = new List<SurfaceLayer>();
                    layers.Add(s.Layer(surface =>
                    {
                        for (var y = 0; y < surface.Height; y++)
                            for (var x = 0; x < surface.Width; x++)
                            {
                                var shade = (byte)(40 + (y * 30 / Math.Max(1, surface.Height - 1)));
                                surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(shade, shade, (byte)(shade + 20)));
                            }
                    }));
                    layers.Add(s.Layer(surface =>
                    {
                        var msg = "KGP + Layers";
                        for (var i = 0; i < msg.Length && i + 2 < surface.Width; i++)
                            surface[i + 2, 2] = new SurfaceCell(msg[i].ToString(), Hex1bColor.White, null);
                    }));
                    return layers;
                })
                .Width(SizeHint.Fixed(30))
                .Height(SizeHint.Fixed(5))
            ).Title("Layered Surface"),
        ]).Height(SizeHint.Content),
        v.Text(""),
        v.Text(" SurfaceLayerContext.CreateKgp() creates tracked KGP objects for layers."),
        v.Text(" CompositeSurface preserves KGP data through layer compositing."),
    ]);
}

Hex1bWidget BuildMultiImageScene<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text(" Multiple KGP images can coexist in a widget tree."),
        v.Text(" Each gets a unique image ID for independent placement."),
        v.Text(""),
        v.HStack(h => [
            h.Text(" "),
            h.Border(h.KgpImage(redImage, 40, 40, "[R]", width: 8, height: 4)).Title("Red"),
            h.Text(" "),
            h.Border(h.KgpImage(greenImage, 40, 40, "[G]", width: 8, height: 4)).Title("Green"),
            h.Text(" "),
            h.Border(h.KgpImage(blueImage, 40, 40, "[B]", width: 8, height: 4)).Title("Blue"),
            h.Text(" "),
            h.Border(h.KgpImage(gradientImage, 80, 40, "[Grad]", width: 8, height: 4)).Title("Gradient"),
        ]).Height(SizeHint.Content),
        v.Text(""),
        v.Text(" Images are content-hash deduplicated: identical data = same image ID."),
        v.Text(" Transmit-once, place-many: efficient bandwidth usage."),
    ]);
}

Hex1bWidget BuildComputedScene<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text(" Computed layers can query KGP images from layers below."),
        v.Text(" This enables effects like dimming, tinting, or overlay markers."),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Border(
                h.Surface(s =>
                {
                    var layers = new List<SurfaceLayer>();
                    layers.Add(s.Layer(surface =>
                    {
                        for (var y = 0; y < surface.Height; y++)
                            for (var x = 0; x < surface.Width; x++)
                            {
                                var r = (byte)(x * 255 / Math.Max(1, surface.Width - 1));
                                var g = (byte)(y * 255 / Math.Max(1, surface.Height - 1));
                                surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, 128));
                            }
                    }));
                    layers.Add(s.Layer(ctx2 =>
                    {
                        var below = ctx2.GetBelow();
                        if ((ctx2.X + ctx2.Y) % 3 == 0 && below.Background is not null)
                            return new SurfaceCell("·", Hex1bColor.White, below.Background);
                        return SurfaceCells.Empty;
                    }));
                    return layers;
                })
                .Width(SizeHint.Fixed(30))
                .Height(SizeHint.Fixed(8))
            ).Title("Computed Layer Effect"),
        ]).Height(SizeHint.Content),
        v.Text(""),
        v.Text(" ComputeContext API: HasKgpBelow(), GetKgpBelow() -> KgpCellAccess"),
        v.Text(" Access: ImageId, CellOffsetX/Y, ZIndex, SourcePixelWidth/Height"),
    ]);
}

// --- Image generation helpers ---

static byte[] GenerateGradientImage(int width, int height)
{
    var data = new byte[width * height * 4];
    for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            data[offset] = (byte)(x * 255 / Math.Max(1, width - 1));
            data[offset + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
            data[offset + 2] = 128;
            data[offset + 3] = 255;
        }
    return data;
}

static byte[] GenerateSolidImage(int width, int height, byte r, byte g, byte b)
{
    var data = new byte[width * height * 4];
    for (var i = 0; i < data.Length; i += 4)
    {
        data[i] = r;
        data[i + 1] = g;
        data[i + 2] = b;
        data[i + 3] = 255;
    }
    return data;
}

static byte[] GenerateCheckerImage(int width, int height)
{
    var data = new byte[width * height * 4];
    for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var val = ((x / 8) + (y / 8)) % 2 == 0 ? (byte)200 : (byte)80;
            data[offset] = val;
            data[offset + 1] = val;
            data[offset + 2] = val;
            data[offset + 3] = 255;
        }
    return data;
}
