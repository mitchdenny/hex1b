
namespace Hex1b.Tests;

/// <summary>
/// Extension methods for test sequences that provide SVG capture functionality.
/// </summary>
public static class TestSequenceExtensions
{
    /// <summary>
    /// Adds a capture step to the sequence. The capture will be saved as an SVG.
    /// </summary>
    /// <param name="builder">The test sequence builder.</param>
    /// <param name="name">Name for this capture (used as filename prefix).</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static Hex1bTerminalInputSequenceBuilder Capture(this Hex1bTerminalInputSequenceBuilder builder, string name = "snapshot")
    {
        builder._steps.Add(new CaptureStep(name));
        return builder;
    }

    /// <summary>
    /// Applies the test sequence to the terminal. Returns the snapshot from the last Capture step,
    /// which is taken BEFORE any subsequent steps (like Ctrl+C) that might clear the screen.
    /// Falls back to a final snapshot if no Capture step exists.
    /// </summary>
    /// <param name="sequence">The test sequence to apply.</param>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A snapshot captured at the Capture step, before Ctrl+C clears the alternate screen.</returns>
    public static async Task<Hex1bTerminalSnapshot> ApplyWithCaptureAsync(
        this Hex1bTerminalInputSequence sequence,
        Hex1bTerminal terminal,
        CancellationToken ct = default)
    {
        var finalSnapshot = await sequence.ApplyAsync(terminal, ct);

        // Return the last CaptureStep's snapshot if available (taken before Ctrl+C)
        var captureStep = sequence.Steps.OfType<CaptureStep>().LastOrDefault();
        return captureStep?.CapturedSnapshot ?? finalSnapshot;
    }
}
