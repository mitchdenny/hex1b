namespace Hex1b.Automation;

/// <summary>Describes a generated playback artifact.</summary>
/// <param name="Kind">The artifact format.</param>
/// <param name="Path">The absolute destination path.</param>
public sealed record TapeArtifact(TapeArtifactKind Kind, string Path);
