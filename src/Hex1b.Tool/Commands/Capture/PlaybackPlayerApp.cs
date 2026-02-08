using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Interactive TUI player for asciinema recordings with playback controls.
/// </summary>
internal sealed class PlaybackPlayerApp
{
    private readonly string _filePath;
    private readonly double _initialSpeed;

    private Hex1bApp? _app;
    private Hex1bTerminal? _playbackTerminal;
    private AsciinemaRecording? _recording;
    private TerminalWidgetHandle? _handle;
    private CancellationTokenSource? _playbackCts;
    private Task? _playbackTask;

    private static readonly double[] Speeds = [0.25, 0.5, 1.0, 1.5, 2.0, 3.0, 5.0];
    private static readonly string[] SpeedLabels = Speeds.Select(s => $"{s}x").ToArray();

    private PlaybackPlayerApp(string filePath, double initialSpeed)
    {
        _filePath = filePath;
        _initialSpeed = initialSpeed;
    }

    public static async Task<int> RunAsync(string filePath, double initialSpeed, CancellationToken cancellationToken)
    {
        var app = new PlaybackPlayerApp(filePath, initialSpeed);
        return await app.RunInternalAsync(cancellationToken);
    }

    private async Task<int> RunInternalAsync(CancellationToken cancellationToken)
    {
        // Load the recording into a headless playback terminal
        await LoadRecordingAsync();

        // Build the display TUI
        await using var displayTerminal = Hex1bTerminal.CreateBuilder()
            .WithMouse()
            .WithHex1bApp((app, options) =>
            {
                _app = app;
                return ctx => BuildWidget(ctx);
            })
            .Build();

        try
        {
            return await displayTerminal.RunAsync(cancellationToken);
        }
        finally
        {
            await StopPlaybackAsync();
        }
    }

    private async Task LoadRecordingAsync()
    {
        await StopPlaybackAsync();

        _playbackTerminal = Hex1bTerminal.CreateBuilder()
            .WithAsciinemaPlayback(_filePath, out var recording, _initialSpeed)
            .WithTerminalWidget(out var handle)
            .Build();

        _recording = recording;
        _handle = handle;

        _recording.PositionChanged += _ => _app?.Invalidate();
        _recording.StateChanged += _ => _app?.Invalidate();

        _playbackCts = new CancellationTokenSource();
        _playbackTask = Task.Run(async () =>
        {
            try
            {
                await _playbackTerminal.RunAsync(_playbackCts.Token);
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task StopPlaybackAsync()
    {
        if (_playbackCts != null)
        {
            await _playbackCts.CancelAsync();
            if (_playbackTask != null)
            {
                try { await _playbackTask; }
                catch (OperationCanceledException) { }
            }
            _playbackCts.Dispose();
            _playbackCts = null;
        }

        if (_playbackTerminal != null)
        {
            await _playbackTerminal.DisposeAsync();
            _playbackTerminal = null;
        }
    }

    private Hex1bWidget BuildWidget(RootContext ctx)
    {
        if (_recording == null || _handle == null)
        {
            return ctx.Text("Loading...");
        }

        var state = _recording.State;
        var position = _recording.CurrentPosition;
        var duration = _recording.Duration;
        var fileName = Path.GetFileName(_filePath);

        var progressText = $"{FormatTime(position)} / {FormatTime(duration)}";
        var progressPercent = duration > 0 ? (int)(position / duration * 100) : 0;

        var defaultSpeedIndex = Array.IndexOf(Speeds, _initialSpeed);
        if (defaultSpeedIndex < 0) defaultSpeedIndex = Array.IndexOf(Speeds, 1.0);

        return ctx.VStack(v =>
        [
            // Terminal display area
            v.Border(
                v.Terminal(_handle).Fill(),
                title: $"{fileName} [{state}]"
            ).Fill(),

            // Controls
            v.Border(
                v.VStack(controls =>
                [
                    // Progress bar
                    controls.HStack(progress =>
                    [
                        progress.Text(progressText).FixedWidth(15),
                        progress.Progress(progressPercent).Fill()
                    ]),

                    // Buttons and speed picker
                    controls.HStack(buttons =>
                    [
                        state == AsciinemaPlaybackState.Playing
                            ? buttons.Button("⏸ Pause").OnClick(_ => _recording.Pause())
                            : buttons.Button("▶ Play").OnClick(_ => _recording.Play()),

                        buttons.Text(" "),

                        buttons.Button("⏮ -10s").OnClick(_ => _recording.Seek(position - 10)),
                        buttons.Button("⏭ +10s").OnClick(_ => _recording.Seek(position + 10)),

                        buttons.Text(" "),

                        buttons.Button("↺ Restart").OnClick(_ =>
                        {
                            _recording.Seek(0);
                            _recording.Play();
                        }),

                        buttons.Text("   Speed: "),

                        (buttons.Picker(SpeedLabels) with { InitialSelectedIndex = defaultSpeedIndex })
                            .OnSelectionChanged(e => _recording.Play(Speeds[e.SelectedIndex])),

                        // Chapters (only if markers exist)
                        ..(_recording.Markers.Count > 0
                            ? [
                                buttons.Text("   Chapter: "),
                                buttons.Picker(_recording.Markers.Select(m => m.Label).ToArray())
                                    .OnSelectionChanged(e =>
                                    {
                                        var marker = _recording.Markers[e.SelectedIndex];
                                        _recording.Seek(marker.Timestamp);
                                        _recording.Play();
                                    })
                              ]
                            : Array.Empty<Hex1bWidget>())
                    ])
                ]),
                title: "Controls"
            ),

            // Status bar
            v.InfoBar([
                "Space", "Play/Pause",
                "←/→", "Seek ±10s",
                "Q", "Quit"
            ])
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Spacebar).Global().Action(_ =>
            {
                if (_recording!.State == AsciinemaPlaybackState.Playing)
                    _recording.Pause();
                else
                    _recording.Play();
            }, "Toggle playback");

            bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ => _recording!.Seek(_recording.CurrentPosition - 10), "Seek back");
            bindings.Key(Hex1bKey.RightArrow).Global().Action(_ => _recording!.Seek(_recording.CurrentPosition + 10), "Seek forward");
            bindings.Key(Hex1bKey.Q).Global().Action(_ => _app?.RequestStop(), "Quit");
        });
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        return $"{(int)(seconds / 60):D2}:{(int)(seconds % 60):D2}";
    }
}
