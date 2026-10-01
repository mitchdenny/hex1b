namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnPrimaryChanged"/>
/// callback. Fires when the primary peer changes (including transitions
/// to no-primary).
/// </summary>
public sealed class Hmp1ServerPrimaryChangedEventArgs : EventArgs
{
    internal Hmp1ServerPrimaryChangedEventArgs(string? primaryPeerId)
    {
        PrimaryPeerId = primaryPeerId;
    }

    /// <summary>The new primary peer ID, or null if no peer is primary.</summary>
    public string? PrimaryPeerId { get; }
}
