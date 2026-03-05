namespace Hex1b;

/// <summary>
/// Specifies the visual style of underline decoration on a terminal cell.
/// Corresponds to SGR 4:x sub-parameter values.
/// </summary>
public enum UnderlineStyle : byte
{
    /// <summary>No underline (SGR 4:0 or SGR 24).</summary>
    None = 0,

    /// <summary>Single straight underline (SGR 4 or SGR 4:1). This is the default when SGR 4 is used without a sub-parameter.</summary>
    Single = 1,

    /// <summary>Double underline (SGR 4:2 or SGR 21).</summary>
    Double = 2,

    /// <summary>Curly/wavy underline (SGR 4:3). Used by editors like Kakoune for diagnostics.</summary>
    Curly = 3,

    /// <summary>Dotted underline (SGR 4:4).</summary>
    Dotted = 4,

    /// <summary>Dashed underline (SGR 4:5).</summary>
    Dashed = 5,
}
