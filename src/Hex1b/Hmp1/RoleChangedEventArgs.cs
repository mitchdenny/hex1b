using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnRoleChanged"/>
/// callback.
/// </summary>
public sealed class RoleChangedEventArgs : EventArgs
{
    internal RoleChangedEventArgs(string? primaryPeerId, int width, int height, string reason, bool previouslyPrimary, bool nowPrimary)
    {
        PrimaryPeerId = primaryPeerId;
        Width = width;
        Height = height;
        Reason = reason;
        PreviouslyPrimary = previouslyPrimary;
        NowPrimary = nowPrimary;
    }

    /// <summary>The new primary's peer ID, or null when no peer is primary.</summary>
    public string? PrimaryPeerId { get; }

    /// <summary>The PTY width as of this transition.</summary>
    public int Width { get; }

    /// <summary>The PTY height as of this transition.</summary>
    public int Height { get; }

    /// <summary>Free-form reason string from the producer.</summary>
    public string Reason { get; }

    /// <summary>Whether the receiving adapter was primary before this transition.</summary>
    public bool PreviouslyPrimary { get; }

    /// <summary>Whether the receiving adapter is primary after this transition.</summary>
    public bool NowPrimary { get; }
}
