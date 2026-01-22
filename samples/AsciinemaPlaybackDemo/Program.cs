using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// AsciinemaPlaybackDemo - Demonstrates playing back asciinema (.cast) recordings
// with interactive playback controls embedded in a Hex1b TUI.
// Features a file browser to select recordings from the current directory.

// Find all .cast files in the current working directory
var castFiles = Directory.GetFiles(Environment.CurrentDirectory, "*.cast")
    .Select(f => new FileInfo(f))
    .OrderBy(f => f.Name)
    .ToList();

// Fallback to the demo.cast in the app directory if no files found
if (castFiles.Count == 0)
{
    var demoFile = Path.Combine(AppContext.BaseDirectory, "demo.cast");
    if (File.Exists(demoFile))
    {
        castFiles.Add(new FileInfo(demoFile));
    }
}

if (castFiles.Count == 0)
{
    Console.Error.WriteLine("Error: No .cast files found in current directory");
    return 1;
}

// Playback speeds available in the picker
var speeds = new[] { 0.25, 0.5, 1.0, 1.5, 2.0, 3.0, 5.0 };
var speedLabels = speeds.Select(s => $"{s}x").ToArray();
var defaultSpeedIndex = Array.IndexOf(speeds, 1.0); // Default to 1x

// Reference to the app for invalidation
Hex1bApp? app = null;

// Current playback state - mutable to allow switching recordings
Hex1bTerminal? playbackTerminal = null;
AsciinemaRecording? recording = null;
TerminalWidgetHandle? terminalHandle = null;
CancellationTokenSource? playbackCts = null;
Task? playbackTask = null;
int selectedFileIndex = 0;

// Load a new recording file
async Task LoadRecordingAsync(string filePath)
{
    // Stop any existing playback
    if (playbackCts != null)
    {
        await playbackCts.CancelAsync();
        if (playbackTask != null)
        {
            try { await playbackTask; } catch (OperationCanceledException) { }
        }
    }
    
    // Dispose existing terminal
    if (playbackTerminal != null)
    {
        await playbackTerminal.DisposeAsync();
    }
    
    // Create new terminal with the selected file
    playbackTerminal = Hex1bTerminal.CreateBuilder()
        .WithAsciinemaPlayback(filePath, out var newRecording)
        .WithTerminalWidget(out var newHandle)
        .Build();
    
    recording = newRecording;
    terminalHandle = newHandle;
    
    // Subscribe to updates
    recording.PositionChanged += _ => app?.Invalidate();
    recording.StateChanged += _ => app?.Invalidate();
    
    // Start playback
    playbackCts = new CancellationTokenSource();
    playbackTask = Task.Run(async () =>
    {
        try
        {
            await playbackTerminal.RunAsync(playbackCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    });
    
    app?.Invalidate();
}

// Load the first file initially
await LoadRecordingAsync(castFiles[0].FullName);

// Build the widget tree with file list, terminal, and controls
Hex1bWidget BuildUI(RootContext ctx)
{
    // Handle case where recording not yet loaded
    if (recording == null || terminalHandle == null)
    {
        return ctx.Text("Loading...");
    }
    
    var state = recording.State;
    var position = recording.CurrentPosition;
    var duration = recording.Duration;
    var currentFile = castFiles[selectedFileIndex].Name;
    
    // Format time as MM:SS
    static string FormatTime(double seconds) => 
        $"{(int)(seconds / 60):D2}:{(int)(seconds % 60):D2}";
    
    var progressText = $"{FormatTime(position)} / {FormatTime(duration)}";
    var progressPercent = duration > 0 ? (int)(position / duration * 100) : 0;
    
    return ctx.HStack(h =>
    [
        // Left panel: File list
        h.Border(
            (h.List(castFiles.Select(f => f.Name).ToArray()) with { InitialSelectedIndex = selectedFileIndex })
                .OnSelectionChanged(async e =>
                {
                    if (e.SelectedIndex != selectedFileIndex)
                    {
                        selectedFileIndex = e.SelectedIndex;
                        await LoadRecordingAsync(castFiles[selectedFileIndex].FullName);
                    }
                }),
            title: "Recordings"
        ).FixedWidth(30),
        
        // Right panel: Terminal and controls
        h.VStack(v =>
        [
            // Main terminal display area
            v.Border(
                v.Terminal(terminalHandle).Fill(),
                title: $"{currentFile} [{state}]"
            ).Fill(),
            
            // Control bar at the bottom
            v.Border(
                v.VStack(controls =>
                [
                    // Progress bar
                    controls.HStack(progress =>
                    [
                        progress.Text(progressText).FixedWidth(15),
                        progress.Progress(progressPercent).Fill()
                    ]),
                
                // Control buttons and speed picker
                controls.HStack(buttons =>
                [
                    // Play/Pause button
                    state == AsciinemaPlaybackState.Playing
                        ? buttons.Button("⏸ Pause").OnClick(_ => recording.Pause())
                        : buttons.Button("▶ Play").OnClick(_ => recording.Play()),
                    
                    buttons.Text(" "),
                    
                    // Seek buttons
                    buttons.Button("⏮ -10s").OnClick(_ => recording.Seek(position - 10)),
                    buttons.Button("⏭ +10s").OnClick(_ => recording.Seek(position + 10)),
                    
                    buttons.Text(" "),
                    
                    // Restart button
                    buttons.Button("↺ Restart").OnClick(_ => 
                    {
                        recording.Seek(0);
                        recording.Play();
                    }),
                    
                    buttons.Text("   Speed: "),
                    
                    // Speed picker
                    (buttons.Picker(speedLabels) with { InitialSelectedIndex = defaultSpeedIndex })
                        .OnSelectionChanged(e => recording.Play(speeds[e.SelectedIndex])),
                    
                    // Chapters picker (only show if there are markers)
                    ..(recording.Markers.Count > 0 
                        ? [
                            buttons.Text("   Chapter: "),
                            buttons.Picker(recording.Markers.Select(m => m.Label).ToArray())
                                .OnSelectionChanged(e => 
                                {
                                    var marker = recording.Markers[e.SelectedIndex];
                                    recording.Seek(marker.Timestamp);
                                    recording.Play();
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
    ]).Fill()  // End of right panel VStack
]).WithInputBindings(bindings =>
{
    // Use Global() so these bindings work even when the terminal widget has focus
    bindings.Key(Hex1bKey.Spacebar).Global().Action(_ =>
    {
        if (recording!.State == AsciinemaPlaybackState.Playing)
            recording.Pause();
        else
            recording.Play();
    }, "Toggle playback");
    
    bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ => recording!.Seek(recording.CurrentPosition - 10), "Seek back");
    bindings.Key(Hex1bKey.RightArrow).Global().Action(_ => recording!.Seek(recording.CurrentPosition + 10), "Seek forward");
    bindings.Key(Hex1bKey.Q).Global().Action(_ => app?.RequestStop(), "Quit");
});
}

// Create the display terminal with the TUI app
using var appCts = new CancellationTokenSource();
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithRenderOptimization()
    .WithMouse()
    .WithHex1bApp((a, options) =>
    {
        app = a;
        return ctx => BuildUI(ctx);
    })
    .Build();

try
{
    await displayTerminal.RunAsync(appCts.Token);
}
finally
{
    // Stop playback
    if (playbackCts != null)
    {
        await playbackCts.CancelAsync();
        if (playbackTask != null)
        {
            try { await playbackTask; } catch (OperationCanceledException) { }
        }
    }
    
    // Dispose the playback terminal
    if (playbackTerminal != null)
    {
        await playbackTerminal.DisposeAsync();
    }
}

return 0;

