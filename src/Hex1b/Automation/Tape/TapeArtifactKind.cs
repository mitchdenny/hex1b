namespace Hex1b.Automation;

/// <summary>Identifies the format of a generated tape artifact.</summary>
public enum TapeArtifactKind
{
    /// <summary>An asciicast v2 terminal recording.</summary>
    Asciicast,
    /// <summary>VHS-compatible text checkpoints separated by horizontal lines.</summary>
    GoldenText
}
