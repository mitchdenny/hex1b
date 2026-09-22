using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnRemoteResized"/> callback.
/// </summary>
public sealed class RemoteResizedEventArgs : EventArgs
{
    internal RemoteResizedEventArgs(int width, int height, bool causedByLocalPrimary)
    {
        Width = width;
        Height = height;
        CausedByLocalPrimary = causedByLocalPrimary;
    }

    /// <summary>The new producer PTY width.</summary>
    public int Width { get; }

    /// <summary>The new producer PTY height.</summary>
    public int Height { get; }

    /// <summary>
    /// <see langword="true"/> when the receiving adapter was primary at the
    /// moment the resize took effect (typically meaning this client caused
    /// the resize via <see cref="Hmp1WorkloadAdapter.RequestPrimaryAsync"/>
    /// or <see cref="Hmp1WorkloadAdapter.ResizeAsync"/>); <see langword="false"/>
    /// when another peer caused it.
    /// </summary>
    public bool CausedByLocalPrimary { get; }
}
