using System.Diagnostics;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

// Sample text and selectable state.
var sampleText = "Hello!";
var fontNames = FigletFonts.Names.ToArray();
var selectedFontIdx = 0;
var horizontalLayoutOptions = new[]
{
    FigletLayoutMode.Default,
    FigletLayoutMode.Smushed,
    FigletLayoutMode.Fitted,
    FigletLayoutMode.FullWidth,
};
var horizontalLayoutLabels = new[] { "Default", "Smushed", "Fitted", "Full width" };
var horizontalLayoutIdx = 0;

var horizontalOverflowOptions = new[] { FigletHorizontalOverflow.Clip, FigletHorizontalOverflow.Wrap };
var horizontalOverflowLabels = new[] { "Clip", "Wrap" };
var horizontalOverflowIdx = 0;

var verticalOverflowOptions = new[] { FigletVerticalOverflow.Clip, FigletVerticalOverflow.Truncate };
var verticalOverflowLabels = new[] { "Clip", "Truncate" };
var verticalOverflowIdx = 0;

var effectNames = new[] { "None", "Rainbow", "H gradient", "V gradient", "Wave", "Shimmer" };
var effectIdx = 0;

var clock = Stopwatch.StartNew();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var font = FigletFont.LoadBundled(fontNames[selectedFontIdx]);
        var preview = ctx.FigletText(sampleText)
            .Font(font)
            .Layout(horizontalLayoutOptions[horizontalLayoutIdx])
            .HorizontalOverflow(horizontalOverflowOptions[horizontalOverflowIdx])
            .VerticalOverflow(verticalOverflowOptions[verticalOverflowIdx]);

        var effect = BuildEffect(effectNames[effectIdx], clock);
        Hex1bWidget previewArea = effect is null
            ? preview
            : ctx.EffectPanel(preview, effect).RedrawAfter(50);

        return ctx.HStack(h =>
        [
            // Left pane: font picker.
            h.VStack(left =>
            [
                left.Text("FIGlet Fonts"),
                left.Text("------------"),
                left.List(fontNames)
                    .OnSelectionChanged(e => selectedFontIdx = e.SelectedIndex)
                    .OnItemActivated(e => selectedFontIdx = e.ActivatedIndex)
                    .FillHeight(),
            ]).FixedWidth(20),
            h.Text(" │ "),
            // Right pane: preview on top, controls beneath.
            h.VStack(right =>
            [
                right.Text($"Sample text:  {sampleText}"),
                right.Text(""),
                right.Border(previewArea).FillHeight(),
                right.Text(""),
                right.Text("─── Controls ────────────────────────────"),
                right.HStack(row =>
                [
                    row.Text("Text:    "),
                    row.TextBox(sampleText)
                        .OnTextChanged(e => sampleText = e.NewText)
                        .FixedWidth(40),
                ]).FixedHeight(1),
                right.HStack(row =>
                [
                    row.Text("Layout:  "),
                    row.Picker(horizontalLayoutLabels)
                        .OnSelectionChanged(e => horizontalLayoutIdx = e.SelectedIndex),
                ]).FixedHeight(1),
                right.HStack(row =>
                [
                    row.Text("H over:  "),
                    row.Picker(horizontalOverflowLabels)
                        .OnSelectionChanged(e => horizontalOverflowIdx = e.SelectedIndex),
                ]).FixedHeight(1),
                right.HStack(row =>
                [
                    row.Text("V over:  "),
                    row.Picker(verticalOverflowLabels)
                        .OnSelectionChanged(e => verticalOverflowIdx = e.SelectedIndex),
                ]).FixedHeight(1),
                right.HStack(row =>
                [
                    row.Text("Effect:  "),
                    row.Picker(effectNames)
                        .OnSelectionChanged(e => effectIdx = e.SelectedIndex),
                ]).FixedHeight(1),
                right.Text(""),
                right.Text("Tab: cycle focus  |  Enter: open picker  |  Ctrl+C: quit"),
            ]).Fill(),
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

static Action<Surface>? BuildEffect(string name, Stopwatch clock) => name switch
{
    "None" => null,
    "Rainbow" => surface => Rainbow(surface),
    "H gradient" => surface => HorizontalGradient(surface),
    "V gradient" => surface => VerticalGradient(surface),
    "Wave" => surface => Wave(surface, clock.Elapsed.TotalSeconds),
    "Shimmer" => surface => Shimmer(surface, clock.Elapsed.TotalSeconds),
    _ => null,
};

static void Rainbow(Surface surface)
{
    for (var y = 0; y < surface.Height; y++)
    {
        for (var x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, y];
            if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
            var hue = (double)x / Math.Max(1, surface.Width);
            surface[x, y] = cell with { Foreground = HsvToColor(hue, 0.85, 1.0) };
        }
    }
}

static void HorizontalGradient(Surface surface)
{
    var start = (R: (byte)64,  G: (byte)156, B: (byte)255);
    var end =   (R: (byte)255, G: (byte)128, B: (byte)64);
    for (var y = 0; y < surface.Height; y++)
    {
        for (var x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, y];
            if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
            var t = (double)x / Math.Max(1, surface.Width - 1);
            surface[x, y] = cell with { Foreground = Lerp(start, end, t) };
        }
    }
}

static void VerticalGradient(Surface surface)
{
    var start = (R: (byte)138, G: (byte)64,  B: (byte)255);
    var end =   (R: (byte)255, G: (byte)200, B: (byte)64);
    for (var y = 0; y < surface.Height; y++)
    {
        var t = (double)y / Math.Max(1, surface.Height - 1);
        var color = Lerp(start, end, t);
        for (var x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, y];
            if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
            surface[x, y] = cell with { Foreground = color };
        }
    }
}

static void Wave(Surface surface, double seconds)
{
    var phase = seconds * 1.2;
    for (var y = 0; y < surface.Height; y++)
    {
        for (var x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, y];
            if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
            var diag = (x + y) / (double)Math.Max(1, surface.Width + surface.Height);
            var brightness = 0.5 + 0.5 * Math.Sin((diag - phase) * Math.PI * 2.0);
            var fg = HsvToColor((diag + phase * 0.5) % 1.0, 0.7, 0.6 + 0.4 * brightness);
            surface[x, y] = cell with { Foreground = fg };
        }
    }
}

static void Shimmer(Surface surface, double seconds)
{
    var pulse = 0.5 + 0.5 * Math.Sin(seconds * 4.0);
    var brightness = (byte)(120 + (int)(135 * pulse));
    var color = Hex1bColor.FromRgb(brightness, brightness, brightness);
    for (var y = 0; y < surface.Height; y++)
    {
        for (var x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, y];
            if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
            surface[x, y] = cell with { Foreground = color };
        }
    }
}

static Hex1bColor HsvToColor(double h, double s, double v)
{
    var i = (int)Math.Floor(h * 6.0) % 6;
    var f = h * 6.0 - Math.Floor(h * 6.0);
    var p = v * (1.0 - s);
    var q = v * (1.0 - f * s);
    var t = v * (1.0 - (1.0 - f) * s);
    var (r, g, b) = i switch
    {
        0 => (v, t, p),
        1 => (q, v, p),
        2 => (p, v, t),
        3 => (p, q, v),
        4 => (t, p, v),
        _ => (v, p, q),
    };
    return Hex1bColor.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
}

static Hex1bColor Lerp((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double t)
{
    var u = Math.Clamp(t, 0.0, 1.0);
    return Hex1bColor.FromRgb(
        (byte)(a.R + (b.R - a.R) * u),
        (byte)(a.G + (b.G - a.G) * u),
        (byte)(a.B + (b.B - a.B) * u));
}
