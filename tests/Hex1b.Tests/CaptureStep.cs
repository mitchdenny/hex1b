using Hex1b.Automation;

namespace Hex1b.Tests;

/// <summary>
/// A test step that captures the current terminal state as SVG and HTML and attaches them to the test.
/// Also saves the snapshot so it can be returned by <see cref="TestSequenceExtensions.ApplyWithCaptureAsync"/>.
/// Waits for the terminal output to stabilize before capturing so that the snapshot
/// reflects the full render, not a partially-flushed frame.
/// </summary>
/// <param name="Name">Name for this capture point (used as filename prefix).</param>
internal sealed record CaptureStep(string Name) : TestStep
{
    /// <summary>
    /// The snapshot captured during execution, before any subsequent steps (like Ctrl+C) run.
    /// </summary>
    internal Hex1bTerminalSnapshot? CapturedSnapshot { get; private set; }

    /// <summary>
    /// Executes the capture step by polling the terminal until the screen content
    /// stabilizes (two consecutive snapshots match), then saving the final snapshot.
    /// </summary>
    internal override async Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        var timeProvider = options.TimeProvider ?? TimeProvider.System;

        // Poll until the screen content is stable (output pump has drained).
        // Max 50 iterations × 50ms = 2.5s worst-case, but typically stabilizes in 1-2 polls.
        string? previousText = null;
        Hex1bTerminalSnapshot? stableSnapshot = null;
        const int maxIterations = 50;

        for (int i = 0; i < maxIterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            stableSnapshot?.Dispose();
            stableSnapshot = terminal.CreateSnapshot();
            var currentText = stableSnapshot.GetScreenText();

            if (currentText == previousText)
                break;

            previousText = currentText;
            await DelayAsync(timeProvider, TimeSpan.FromMilliseconds(50), ct);
        }

        CapturedSnapshot = stableSnapshot;
        TestCaptureHelper.Capture(terminal, Name);
    }
}
