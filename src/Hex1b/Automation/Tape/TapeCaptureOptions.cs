namespace Hex1b.Automation;

/// <summary>Selects playback artifacts without changing the Tape language.</summary>
public sealed class TapeCaptureOptions
{
    /// <summary>Gets the destination for an asciicast v2 recording, or null to disable it.</summary>
    public string? AsciinemaPath { get; init; }

    /// <summary>Gets a destination for golden text, overriding tape text-output destinations when supplied.</summary>
    public string? GoldenTextPath { get; init; }

    /// <summary>Gets whether existing artifact files may be replaced. The default is false.</summary>
    public bool Overwrite { get; init; }
}
