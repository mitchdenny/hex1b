using Hex1b;
using Hex1b.Widgets;

// ── Bouncing Icons Demo ─────────────────────────────────────────────────
// Icons float around the terminal, bouncing off walls.
// Hover an icon to freeze it. Click to display its name.

var state = new BouncingState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        state.App = app;
        state.Start();

        return ctx => ctx.VStack(v =>
        {
            var widgets = new List<Hex1bWidget>();

            for (int i = 0; i < state.Icons.Count; i++)
            {
                var icon = state.Icons[i];

                widgets.Add(v.Float(
                    v.Interactable(ic =>
                        ic.Text(ic.IsHovered ? $"[{icon.Emoji}]" : icon.Emoji)
                    )
                    .OnHoverChanged(e =>
                    {
                        icon.Frozen = e.IsHovered;
                    })
                    .OnClick(_ =>
                    {
                        state.LastClicked = $"Clicked: {icon.Name}";
                    })
                ).Absolute((int)icon.X, (int)icon.Y));
            }

            // Floating status text
            widgets.Add(v.Float(
                v.Text(state.LastClicked ?? "Hover an icon, click to identify")
            ).Absolute((int)state.StatusX, (int)state.StatusY));

            return [.. widgets];
        }).RedrawAfter(state.FrameMs);
    })
    .Build();

await terminal.RunAsync();

// ── State ───────────────────────────────────────────────────────────────

class BouncingIcon
{
    public required string Emoji { get; init; }
    public required string Name { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    public bool Frozen { get; set; }
}

class BouncingState
{
    public Hex1bApp? App { get; set; }
    public string? LastClicked { get; set; }
    public int FrameMs => 120;

    // Status text position & velocity
    public double StatusX { get; set; } = 5;
    public double StatusY { get; set; } = 3;
    private double _statusDx = 0.4;
    private double _statusDy = 0.15;

    public List<BouncingIcon> Icons { get; } =
    [
        new() { Emoji = "🚀", Name = "Rocket",    X =  3, Y =  2, Dx =  0.6, Dy =  0.2  },
        new() { Emoji = "🌟", Name = "Star",      X = 20, Y =  5, Dx = -0.5, Dy =  0.3  },
        new() { Emoji = "🎯", Name = "Target",    X = 40, Y =  8, Dx =  0.4, Dy = -0.25 },
        new() { Emoji = "🔥", Name = "Fire",      X = 15, Y = 12, Dx = -0.3, Dy = -0.2  },
        new() { Emoji = "⚡", Name = "Lightning", X = 55, Y =  3, Dx =  0.35, Dy = 0.3  },
        new() { Emoji = "🎵", Name = "Music",     X = 30, Y = 10, Dx = -0.45, Dy = 0.15 },
        new() { Emoji = "💎", Name = "Diamond",   X = 50, Y = 15, Dx =  0.3, Dy = -0.3  },
        new() { Emoji = "🌈", Name = "Rainbow",   X = 10, Y =  7, Dx =  0.5, Dy = -0.15 },
    ];

    private bool _running;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _ = AnimationLoopAsync();
    }

    private async Task AnimationLoopAsync()
    {
        // Assume a reasonable default; will be bounded by actual terminal size
        int width = 78;
        int height = 22;

        while (_running)
        {
            await Task.Delay(FrameMs);

            foreach (var icon in Icons)
            {
                if (icon.Frozen) continue;

                icon.X += icon.Dx;
                icon.Y += icon.Dy;

                // Bounce off walls (emoji is ~2 chars wide)
                if (icon.X <= 0) { icon.X = 0; icon.Dx = Math.Abs(icon.Dx); }
                if (icon.X >= width - 2) { icon.X = width - 2; icon.Dx = -Math.Abs(icon.Dx); }
                if (icon.Y <= 0) { icon.Y = 0; icon.Dy = Math.Abs(icon.Dy); }
                if (icon.Y >= height - 1) { icon.Y = height - 1; icon.Dy = -Math.Abs(icon.Dy); }
            }

            // Move status text too
            StatusX += _statusDx;
            StatusY += _statusDy;

            int statusLen = (LastClicked ?? "Hover an icon, click to identify").Length;
            if (StatusX <= 0) { StatusX = 0; _statusDx = Math.Abs(_statusDx); }
            if (StatusX >= width - statusLen) { StatusX = Math.Max(0, width - statusLen); _statusDx = -Math.Abs(_statusDx); }
            if (StatusY <= 0) { StatusY = 0; _statusDy = Math.Abs(_statusDy); }
            if (StatusY >= height - 1) { StatusY = height - 1; _statusDy = -Math.Abs(_statusDy); }

            App?.Invalidate();
        }
    }
}
