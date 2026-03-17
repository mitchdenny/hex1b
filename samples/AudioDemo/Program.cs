using AudioDemo;
using Hex1b;
using Hex1b.Widgets;

// ── Audio Setup ──
var fireWav = AudioSamples.GenerateFireCrackle(2.0f);
var waterWav = AudioSamples.GenerateWaterDrip(2.0f);
var metalWav = AudioSamples.GenerateMetalClang(1.0f);
var humWav = AudioSamples.GenerateAmbientHum(3.0f);

var mixer = new AudioMixer();
var consoleAdapter = new ConsolePresentationAdapter(enableMouse: true);
var audioAdapter = new AudioPresentationAdapter(consoleAdapter, mixer);

mixer.Start();

const uint FireClipId = 1;
const uint WaterClipId = 2;
const uint MetalClipId = 3;
const uint HumClipId = 4;

mixer.StoreClip(FireClipId, fireWav);
mixer.StoreClip(WaterClipId, waterWav);
mixer.StoreClip(MetalClipId, metalWav);
mixer.StoreClip(HumClipId, humWav);

// ── Dungeon Map ──
const int MapWidth = 40;
const int MapHeight = 20;

var map = new char[MapHeight, MapWidth];
InitializeMap(map);

// Audio producers with their map positions
var producers = new (int Col, int Row, uint ClipId, string Label, char Glyph)[]
{
    (5, 3, FireClipId, "Torch", '*'),
    (15, 3, FireClipId, "Torch", '*'),
    (30, 5, WaterClipId, "Stream", '~'),
    (31, 6, WaterClipId, "Stream", '~'),
    (32, 7, WaterClipId, "Stream", '~'),
    (10, 15, MetalClipId, "Anvil", '!'),
    (20, 10, HumClipId, "Portal", '@'),
};

// Place all audio producers in the mixer with looping
for (var idx = 0; idx < producers.Length; idx++)
{
    var p = producers[idx];
    mixer.PlaceProducer(p.ClipId, (uint)(idx + 1),
        p.Col, p.Row, 40, loop: true);
}

// Start listener at map center so audio is audible immediately
mixer.ListenerCol = MapWidth / 2;
mixer.ListenerRow = MapHeight / 2;
mixer.UpdateAllVolumes();

// ── Terminal Setup ──
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithPresentation(audioAdapter)
    .WithHex1bApp((app, options) =>
    {
        return ctx =>
        {
            return ctx.VStack(v =>
            {
                var widgets = new List<Hex1bWidget>();

                widgets.Add(v.Text("\x1b[38;2;100;200;255m 🎵 Spatial Audio Demo — Move mouse to change listener position\x1b[0m"));
                widgets.Add(v.Separator());

                // Render the map
                for (var y = 0; y < MapHeight; y++)
                {
                    var line = new System.Text.StringBuilder(" ");
                    for (var x = 0; x < MapWidth; x++)
                    {
                        // Check if this position has an audio producer
                        var isProducer = false;
                        foreach (var p in producers)
                        {
                            if (p.Col == x && p.Row == y)
                            {
                                var color = p.ClipId switch
                                {
                                    FireClipId => "255;150;50",
                                    WaterClipId => "100;150;255",
                                    MetalClipId => "200;200;200",
                                    HumClipId => "180;100;255",
                                    _ => "255;255;255"
                                };
                                line.Append($"\x1b[1;38;2;{color}m{p.Glyph}\x1b[0m");
                                isProducer = true;
                                break;
                            }
                        }

                        if (!isProducer)
                        {
                            var ch = map[y, x];
                            var ansi = ch switch
                            {
                                '#' => $"\x1b[38;2;100;100;100;48;2;40;40;40m{ch}\x1b[0m",
                                '.' => $"\x1b[38;2;80;80;80m{ch}\x1b[0m",
                                _ => ch.ToString()
                            };
                            line.Append(ansi);
                        }
                    }

                    widgets.Add(v.Text(line.ToString()));
                }

                widgets.Add(v.Separator());
                widgets.Add(v.Text(" \x1b[1;38;2;255;150;50m*\x1b[0m=Torch  \x1b[1;38;2;100;150;255m~\x1b[0m=Water  \x1b[1;38;2;200;200;200m!\x1b[0m=Anvil  \x1b[1;38;2;180;100;255m@\x1b[0m=Portal  \x1b[38;2;100;100;100m#\x1b[0m=Wall  \x1b[38;2;80;80;80m.\x1b[0m=Floor"));
                widgets.Add(v.Text("\x1b[38;2;120;120;120m Mouse cursor = listener position. Sound volume changes with proximity.\x1b[0m"));
                widgets.Add(v.Text("\x1b[38;2;120;120;120m Press Escape or Ctrl+C to quit.\x1b[0m"));

                return widgets.ToArray();
            });
        };
    })
    .WithMouse()
    .Build();

try
{
    await terminal.RunAsync();
}
finally
{
    mixer.Dispose();
}

// ── Helpers ──

static void InitializeMap(char[,] map)
{
    for (var y = 0; y < MapHeight; y++)
        for (var x = 0; x < MapWidth; x++)
            map[y, x] = '#';

    CarveRoom(map, 2, 1, 18, 6);
    CarveRoom(map, 22, 1, 16, 10);
    CarveRoom(map, 2, 9, 16, 10);
    CarveRoom(map, 18, 8, 8, 6);

    for (var x = 18; x <= 22; x++) map[4, x] = '.';
    for (var y = 6; y <= 9; y++) map[y, 10] = '.';
    for (var x = 16; x <= 20; x++) map[10, x] = '.';
}

static void CarveRoom(char[,] map, int x, int y, int w, int h)
{
    for (var dy = 1; dy < h - 1; dy++)
        for (var dx = 1; dx < w - 1; dx++)
        {
            var mx = x + dx;
            var my = y + dy;
            if (mx < MapWidth && my < MapHeight)
                map[my, mx] = '.';
        }
}
