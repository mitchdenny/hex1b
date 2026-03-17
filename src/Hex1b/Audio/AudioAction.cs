namespace Hex1b;

/// <summary>
/// The action type for an audio protocol command.
/// Specified by the 'a' key in the control data of an ESC_A sequence.
/// </summary>
public enum AudioAction
{
    /// <summary>Transmit audio clip data for storage (a=t).</summary>
    Transmit,

    /// <summary>Place an audio producer at a cell position (a=p).</summary>
    Place,

    /// <summary>Stop a specific audio placement (a=s).</summary>
    Stop,

    /// <summary>Delete audio clips or placements (a=d).</summary>
    Delete,
}
