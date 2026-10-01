using System.Threading.Channels;

namespace Hex1b.Diagnostics;

/// <summary>
/// The type of frame sent through an attach session.
/// </summary>
public enum AttachFrameType
{
    /// <summary>Raw terminal output (ANSI data).</summary>
    Output,

    /// <summary>Terminal was resized. Data is "cols,rows".</summary>
    Resize,

    /// <summary>Leader status changed. Data is "true" or "false".</summary>
    LeaderChanged,

    /// <summary>Terminal session ended.</summary>
    Exit
}
