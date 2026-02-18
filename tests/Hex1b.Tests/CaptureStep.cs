using Hex1b.Automation;

namespace Hex1b.Tests;

/// <summary>
/// A test step that captures the current terminal state as SVG and HTML and attaches them to the test.
/// Also saves the snapshot so it can be returned by <see cref="TestSequenceExtensions.ApplyWithCaptureAsync"/>.
/// </summary>
/// <param name="Name">Name for this capture point (used as filename prefix).</param>
internal sealed record CaptureStep(string Name) : TestStep
{
    /// <summary>
    /// The snapshot captured during execution, before any subsequent steps (like Ctrl+C) run.
    /// </summary>
    internal Hex1bTerminalSnapshot? CapturedSnapshot { get; private set; }

    /// <summary>
    /// Executes the capture step by taking a snapshot and saving it as SVG and HTML.
    /// </summary>
    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        CapturedSnapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(terminal, Name);
        return Task.CompletedTask;
    }
}
