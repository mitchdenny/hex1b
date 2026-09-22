namespace Hex1b;

/// <summary>
/// Arguments for the <see cref="Hmp1ServerOptions.OnResized"/> callback.
/// Fires when the producer's PTY dimensions change.
/// </summary>
public sealed class Hmp1ServerResizedEventArgs : EventArgs
{
    internal Hmp1ServerResizedEventArgs(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>The new PTY width.</summary>
    public int Width { get; }

    /// <summary>The new PTY height.</summary>
    public int Height { get; }
}
