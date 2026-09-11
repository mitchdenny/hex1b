/// <summary>
/// Turns a frame of the dust cloud into the raw escape sequences that paint it.
/// </summary>
/// <remarks>
/// The simulation in <see cref="DustCloud"/> never emits bytes, and
/// <see cref="CloudWorkloadAdapter"/> never decides what those bytes look like.
/// This interface is the seam between them, which is what lets the same cloud be
/// painted as Sixel rasters or as KGP placements without either demo reimplementing
/// physics, pacing, or input handling.
/// </remarks>
internal interface ICloudRenderer
{
    /// <summary>
    /// Writes the escape sequences that paint <paramref name="cloud"/> for one frame.
    /// </summary>
    /// <param name="cloud">The simulation state to paint.</param>
    /// <param name="columns">Terminal width in cells.</param>
    /// <param name="rows">Terminal height in cells.</param>
    byte[] RenderFrame(DustCloud cloud, int columns, int rows);
}
