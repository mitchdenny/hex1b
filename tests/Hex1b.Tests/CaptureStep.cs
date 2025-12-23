using Hex1b.Terminal.Testing;

namespace Hex1b.Tests;

/// <summary>
/// A test step that captures the current terminal state as an SVG and attaches it to the test.
/// </summary>
/// <param name="Name">Name for this capture point (used as filename prefix).</param>
internal sealed record CaptureStep(string Name) : TestStep
{
    /// <summary>
    /// Executes the capture step by taking a snapshot and saving it as SVG.
    /// </summary>
    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTestSequenceOptions options,
        CancellationToken ct)
    {
        TestSvgHelper.CaptureSvg(terminal, Name);
        return Task.CompletedTask;
    }
}
