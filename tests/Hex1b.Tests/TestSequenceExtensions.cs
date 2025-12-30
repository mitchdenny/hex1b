using Hex1b.Terminal.Automation;

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
    /// Applies the test sequence to the terminal. Any Capture steps will automatically save SVGs.
    /// </summary>
    /// <param name="sequence">The test sequence to apply.</param>
    /// <param name="terminal">The terminal to apply the sequence to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A snapshot of the terminal state after all steps have been executed.</returns>
    public static Task<Hex1bTerminalSnapshot> ApplyWithCaptureAsync(
        this Hex1bTerminalInputSequence sequence,
        Hex1bTerminal terminal,
        CancellationToken ct = default)
    {
        // CaptureStep now handles its own capture logic, so we just use regular ApplyAsync
        return sequence.ApplyAsync(terminal, ct);
    }
}
