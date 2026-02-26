namespace Hex1b.Kgp;

/// <summary>
/// The action type for a KGP (Kitty Graphics Protocol) command.
/// Specified by the 'a' key in the control data.
/// </summary>
public enum KgpAction
{
    /// <summary>Transmit image data without displaying (a=t, default).</summary>
    Transmit,

    /// <summary>Transmit image data and display immediately (a=T).</summary>
    TransmitAndDisplay,

    /// <summary>Query terminal support without storing image (a=q).</summary>
    Query,

    /// <summary>Display a previously transmitted image (a=p).</summary>
    Put,

    /// <summary>Delete images or placements (a=d).</summary>
    Delete,

    /// <summary>Transmit animation frame data (a=f).</summary>
    AnimationFrame,

    /// <summary>Control animation playback (a=a).</summary>
    AnimationControl,

    /// <summary>Compose animation frames (a=c).</summary>
    Compose,
}
