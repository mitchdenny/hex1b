namespace Hex1b;

/// <summary>
/// The deletion target specifier for audio delete commands.
/// Specified by the 'd' key in the control data.
/// </summary>
public enum AudioDeleteTarget
{
    /// <summary>Delete all audio clips and placements (d=a).</summary>
    All,

    /// <summary>Delete by audio clip ID (d=i).</summary>
    ById,
}
