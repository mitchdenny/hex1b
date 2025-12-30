using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// A test step that captures the current terminal state as SVG and HTML and attaches them to the test.
/// </summary>
/// <param name="Name">Name for this capture point (used as filename prefix).</param>
internal sealed record CaptureStep(string Name) : TestStep
{
    /// <summary>
    /// Executes the capture step by taking a snapshot and saving it as SVG and HTML.
    /// </summary>
    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        TestCaptureHelper.Capture(terminal, Name);
        return Task.CompletedTask;
    }
}
