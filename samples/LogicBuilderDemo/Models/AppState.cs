namespace LogicBuilderDemo.Models;

/// <summary>
/// Mutable application state for the Logic Builder.
/// </summary>
public class AppState
{
    public List<Track> Tracks { get; } = [new Track("Track 1")];
    public List<Command> PaletteCommands { get; } = [.. Command.Defaults];
    public List<SavedSequence> SavedSequences { get; } = [];
    public Turtle Turtle { get; } = new();

    /// <summary>Dialog state for naming a sequence when saving to palette.</summary>
    public string SaveDialogName { get; set; } = "";
    public Track? PendingSaveTrack { get; set; }

    /// <summary>Tracks which step is currently being dragged (track + index), so siblings can hide.</summary>
    public (Track Track, int Index)? DraggingStep { get; set; }

    // -- Execution state --

    /// <summary>Whether the program is currently executing step-by-step.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Flattened list of steps to execute, built when Run is pressed.</summary>
    public List<(int TrackIndex, int StepIndex, ITrackStep Step)> ExecutionPlan { get; } = [];

    /// <summary>Current position in the execution plan (index into ExecutionPlan).</summary>
    public int ExecutionCursor { get; set; }

    /// <summary>Reference to the app for triggering re-renders from the timer.</summary>
    public Hex1b.Hex1bApp? App { get; set; }

    private CancellationTokenSource? _executionCts;

    public void AddTrack()
    {
        Tracks.Add(new Track($"Track {Tracks.Count + 1}"));
    }

    public void SaveTrackAsSequence(string name, IReadOnlyList<ITrackStep> steps)
    {
        SavedSequences.Add(new SavedSequence(name, [.. steps]));
    }

    /// <summary>Start stepped execution of the program.</summary>
    public void StartExecution()
    {
        if (IsRunning) return;

        Turtle.Reset();
        ExecutionPlan.Clear();
        ExecutionCursor = 0;

        // Flatten all tracks into the execution plan
        for (int t = 0; t < Tracks.Count; t++)
        {
            var track = Tracks[t];
            FlattenSteps(t, track.Steps);
        }

        if (ExecutionPlan.Count == 0) return;

        IsRunning = true;
        _executionCts = new CancellationTokenSource();
        _ = RunExecutionLoopAsync(_executionCts.Token);
    }

    private void FlattenSteps(int trackIndex, IReadOnlyList<ITrackStep> steps)
    {
        for (int s = 0; s < steps.Count; s++)
        {
            var step = steps[s];
            if (step is SavedSequence seq)
            {
                // Expand subroutines inline but keep the track/step index for highlighting
                ExecutionPlan.Add((trackIndex, s, step));
                foreach (var inner in seq.Steps)
                    FlattenSteps(trackIndex, [inner]);
            }
            else
            {
                ExecutionPlan.Add((trackIndex, s, step));
            }
        }
    }

    private async Task RunExecutionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ExecutionCursor < ExecutionPlan.Count)
        {
            var (_, _, step) = ExecutionPlan[ExecutionCursor];
            Turtle.Execute(step);
            App?.Invalidate();

            ExecutionCursor++;
            if (ExecutionCursor >= ExecutionPlan.Count)
                break;

            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { break; }
        }

        IsRunning = false;
        App?.Invalidate();
    }

    /// <summary>Stop execution early.</summary>
    public void StopExecution()
    {
        _executionCts?.Cancel();
        _executionCts = null;
        IsRunning = false;
    }

    /// <summary>Reset turtle and stop any running execution.</summary>
    public void ResetAll()
    {
        StopExecution();
        Turtle.Reset();
        ExecutionPlan.Clear();
        ExecutionCursor = 0;
    }

    /// <summary>
    /// Returns the (trackIndex, stepIndex) of the currently executing step, or null if not running.
    /// </summary>
    public (int TrackIndex, int StepIndex)? CurrentExecutionPosition =>
        IsRunning && ExecutionCursor < ExecutionPlan.Count
            ? (ExecutionPlan[ExecutionCursor].TrackIndex, ExecutionPlan[ExecutionCursor].StepIndex)
            : null;
}

/// <summary>
/// A saved reusable command sequence in the palette.
/// When placed on a track it acts as a subroutine (not expanded).
/// </summary>
public record SavedSequence(string Name, IReadOnlyList<ITrackStep> Steps) : ITrackStep
{
    public string Glyph => "S";
    public int Cost => Steps.Sum(s => s.Cost);

    /// <summary>Renders like a command: SNAME# where S=subroutine marker.</summary>
    public string Display => $"S{Name}{Cost}";
}
