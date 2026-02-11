using Hex1b;
using Hex1b.Theming;

// Audio mixer values
var master = 80.0;
var music = 60.0;
var effects = 90.0;
var voice = 75.0;

// RGB color picker values
var red = 128.0;
var green = 64.0;
var blue = 192.0;

// Temperature control
var temperature = 22.0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Slider Demo"),
        v.Separator(),
        v.Text(""),

        // Audio Mixer Section
        v.Border(b => [
            b.VStack(inner => [
                inner.HStack(h => [
                    h.Text("Master:  ").FixedWidth(10),
                    h.Slider(master).OnValueChanged(e => master = e.Value).FillWidth(),
                    h.Text($" {master,3:F0}%").FixedWidth(6)
                ]),
                inner.HStack(h => [
                    h.Text("Music:   ").FixedWidth(10),
                    h.Slider(music).OnValueChanged(e => music = e.Value).FillWidth(),
                    h.Text($" {music,3:F0}%").FixedWidth(6)
                ]),
                inner.HStack(h => [
                    h.Text("Effects: ").FixedWidth(10),
                    h.Slider(effects).OnValueChanged(e => effects = e.Value).FillWidth(),
                    h.Text($" {effects,3:F0}%").FixedWidth(6)
                ]),
                inner.HStack(h => [
                    h.Text("Voice:   ").FixedWidth(10),
                    h.Slider(voice).OnValueChanged(e => voice = e.Value).FillWidth(),
                    h.Text($" {voice,3:F0}%").FixedWidth(6)
                ])
            ])
        ]).Title("Audio Mixer"),
        v.Text(""),

        // RGB Color Picker Section
        v.Border(b => [
            b.VStack(inner => [
                inner.HStack(h => [
                    h.ThemePanel(t => { t.Set(SliderTheme.FocusedHandleForegroundColor, Hex1bColor.Red); return t; },
                        tp => [tp.Text("R: ").FixedWidth(3), tp.Slider(red, 0, 255).OnValueChanged(e => red = e.Value).FillWidth()]),
                    h.Text($" {red,3:F0}").FixedWidth(5)
                ]),
                inner.HStack(h => [
                    h.ThemePanel(t => { t.Set(SliderTheme.FocusedHandleForegroundColor, Hex1bColor.Green); return t; },
                        tp => [tp.Text("G: ").FixedWidth(3), tp.Slider(green, 0, 255).OnValueChanged(e => green = e.Value).FillWidth()]),
                    h.Text($" {green,3:F0}").FixedWidth(5)
                ]),
                inner.HStack(h => [
                    h.ThemePanel(t => { t.Set(SliderTheme.FocusedHandleForegroundColor, Hex1bColor.Blue); return t; },
                        tp => [tp.Text("B: ").FixedWidth(3), tp.Slider(blue, 0, 255).OnValueChanged(e => blue = e.Value).FillWidth()]),
                    h.Text($" {blue,3:F0}").FixedWidth(5)
                ]),
                inner.Text(""),
                inner.ThemePanel(
                    t => { t.Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb((byte)red, (byte)green, (byte)blue)); return t; },
                    tp => [tp.Text("Preview: ████████")])
            ])
        ]).Title("RGB Color Picker (0-255)"),
        v.Text(""),

        // Temperature Control Section  
        v.Border(b => [
            b.VStack(inner => [
                inner.HStack(h => [
                    h.Text("Temperature: ").FixedWidth(14),
                    h.Slider(temperature, 16, 30, step: 0.5).OnValueChanged(e => temperature = e.Value).FillWidth(),
                    h.Text($" {temperature:F1}°C").FixedWidth(8)
                ]),
                inner.Text(""),
                inner.Text(GetTemperatureStatus(temperature))
            ])
        ]).Title("Thermostat (16-30°C, 0.5° steps)"),
        v.Text(""),

        v.Separator(),
        v.Text("Tab: Navigate | ←→: Adjust | Home/End: Min/Max | PageUp/Down: ±10%"),
        v.Text("Press Ctrl+C to exit")
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();

static string GetTemperatureStatus(double temp) => temp switch
{
    < 18 => "❄️  Too cold - consider warming up",
    < 20 => "🌡️  Cool - comfortable for some",
    < 24 => "✅ Comfortable temperature range",
    < 27 => "🌡️  Warm - comfortable for some",
    _ => "🔥 Hot - consider cooling down"
};
