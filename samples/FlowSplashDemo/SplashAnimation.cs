// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from microsoft/aspire's Aspire.Cli/Tui/AdmireSplashApp.cs.

using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FlowSplashDemo;

internal sealed class SplashAnimation
{
    private const int FormationDurationMs = 2200;
    private const int CaptionFadeDurationMs = 800;
    internal const int TotalDurationMs = FormationDurationMs + CaptionFadeDurationMs + 600;
    private const int LogoWidth = 40;
    private const int LogoHeight = 20;
    private readonly BrailleDot[] _dots = CreateDots();

    public Hex1bWidget Build(RootContext ctx, double elapsedMs)
        => ctx.Surface(layers => [layers.Layer(surface => Render(surface, elapsedMs))]).Fill();

    private void Render(Surface surface, double elapsedMs)
    {
        var scale = Math.Min(1f, Math.Min(surface.Width / (float)LogoWidth,
            Math.Max(0, surface.Height - 3) / (float)LogoHeight));
        var width = (int)(LogoWidth * scale);
        var height = (int)(LogoHeight * scale);
        var offsetX = (surface.Width - width) / 2;
        var offsetY = Math.Max(0, (surface.Height - height - 2) / 2);
        var progress = Math.Clamp((float)(elapsedMs / FormationDurationMs), 0, 1);
        var eased = 1 - MathF.Pow(1 - progress, 3);
        var cells = new Dictionary<(int X, int Y), (int Pattern, int R, int G, int B, int Count)>();

        ReadOnlySpan<int> bits = [0, 1, 2, 6, 3, 4, 5, 7];
        foreach (var dot in _dots)
        {
            var radius = dot.StartRadius * (1 - eased) * scale;
            var angle = dot.StartAngle + dot.Rotations * 2 * MathF.PI * (1 - eased);
            var bx = (int)MathF.Round(dot.X * scale + offsetX * 2 + radius * MathF.Cos(angle));
            var by = (int)MathF.Round(dot.Y * scale + offsetY * 4 + radius * MathF.Sin(angle));
            if (bx < 0 || bx >= surface.Width * 2 || by < 0 || by >= surface.Height * 4 || scale == 0)
                continue;

            // Braille packs two columns of four dots: left bits 0,1,2,6; right 3,4,5,7.
            var bit = 1 << bits[bx % 2 * 4 + by % 4];
            var key = (bx / 2, by / 4);
            cells.TryGetValue(key, out var cell);
            cells[key] = (cell.Pattern | bit, cell.R + dot.R, cell.G + dot.G, cell.B + dot.B, cell.Count + 1);
        }

        foreach (var ((x, y), cell) in cells)
        {
            surface.WriteChar(x, y, (char)(0x2800 | cell.Pattern),
                Hex1bColor.FromRgb((byte)(cell.R / cell.Count), (byte)(cell.G / cell.Count), (byte)(cell.B / cell.Count)));
        }

        var fade = Math.Clamp((float)((elapsedMs - FormationDurationMs) / CaptionFadeDurationMs), 0, 1);
        if (fade > 0)
        {
            const string caption = "Hex1b";
            var x = Math.Max(0, (surface.Width - caption.Length) / 2);
            var y = Math.Min(surface.Height - 1, offsetY + height + 1);
            var intensity = (byte)(230 * fade);
            surface.WriteText(x, y, caption, Hex1bColor.FromRgb(intensity, intensity, intensity));
        }
    }

    private static BrailleDot[] CreateDots()
    {
        // Pointy hexagon and "1b" from src/content/public/logo.svg, sampled as dots.
        (float X, float Y)[] vertices = [(40, 4), (75, 22), (75, 58), (40, 76), (5, 58), (5, 22)];
        string[] lettering = [
            "00100 10000",
            "01100 10000",
            "00100 10110",
            "00100 11001",
            "00100 10001",
            "00100 10001",
            "01110 11110"
        ];
        var dots = new List<BrailleDot>();
        var random = new Random(99);
        for (var y = 0; y < 80; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                var onOutline = false;
                for (var edge = 0; edge < vertices.Length; edge++)
                {
                    var a = vertices[edge];
                    var b = vertices[(edge + 1) % vertices.Length];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                    var distanceX = x - (a.X + t * dx);
                    var distanceY = y - (a.Y + t * dy);
                    onOutline |= distanceX * distanceX + distanceY * distanceY <= 2;
                }

                var textX = (x - 18) / 4;
                var textY = (y - 26) / 4;
                var onLetter = x >= 18 && y >= 26 && textX < lettering[0].Length
                    && textY < lettering.Length && lettering[textY][textX] == '1';
                if (!onOutline && !onLetter)
                    continue;

                var gradient = (x + y) / 160f;
                dots.Add(new BrailleDot(x, y,
                    (byte)(78 - 10 * gradient), (byte)(205 - 37 * gradient), (byte)(196 - 17 * gradient),
                    (float)(40 + random.NextDouble() * 60),
                    (float)(random.NextDouble() * Math.PI * 2),
                    (float)(1.5 + random.NextDouble() * 2.5)));
            }
        }
        return dots.ToArray();
    }

    private readonly record struct BrailleDot(int X, int Y, byte R, byte G, byte B,
        float StartRadius, float StartAngle, float Rotations);
}
