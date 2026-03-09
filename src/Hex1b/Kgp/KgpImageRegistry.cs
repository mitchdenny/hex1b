using Hex1b.Layout;

namespace Hex1b.Kgp;

/// <summary>
/// Collects KGP image positions and window occluder rectangles during rendering.
/// </summary>
/// <remarks>
/// <para>
/// This registry captures KGP image information <b>during rendering</b>, before
/// compositing can overwrite anchor cells. Each image is recorded with its absolute
/// position and rendering layer (z-level). Window bounds are recorded as occluder
/// rectangles. After rendering, the <see cref="KgpOcclusionSolver"/> uses this data
/// to compute visible fragments.
/// </para>
/// <para>
/// The registry is cleared at the start of each frame and populated by:
/// <list type="bullet">
///   <item><see cref="Hex1b.Surfaces.SurfaceRenderContext.WriteKgp"/> — registers images</item>
///   <item><see cref="Hex1b.Nodes.WindowPanelNode.Render"/> — registers window occluders</item>
/// </list>
/// </para>
/// </remarks>
internal class KgpImageRegistry
{
    private readonly List<KgpImageEntry> _images = [];
    private readonly List<OccluderEntry> _occluders = [];
    private int _currentLayer;

    /// <summary>
    /// Registers a KGP image rendered at the given absolute position and current layer.
    /// </summary>
    public void RegisterImage(KgpCellData data, int absoluteX, int absoluteY)
    {
        _images.Add(new KgpImageEntry(data, absoluteX, absoluteY, _currentLayer));
    }

    /// <summary>
    /// Registers a rectangular occluder (typically a window's bounds) at the given layer.
    /// Any KGP image at a lower layer that overlaps this rectangle will be shredded.
    /// </summary>
    public void RegisterOccluder(int x, int y, int width, int height)
    {
        _occluders.Add(new OccluderEntry(_currentLayer, new Rect(x, y, width, height)));
    }

    /// <summary>
    /// Advances to the next rendering layer. Call before rendering each window
    /// so that images and occluders are assigned the correct z-level.
    /// </summary>
    public void PushLayer()
    {
        _currentLayer++;
    }

    /// <summary>
    /// Gets the current rendering layer.
    /// </summary>
    public int CurrentLayer => _currentLayer;

    /// <summary>
    /// Gets all registered KGP images.
    /// </summary>
    public IReadOnlyList<KgpImageEntry> Images => _images;

    /// <summary>
    /// Gets all registered occluder rectangles.
    /// </summary>
    public IReadOnlyList<OccluderEntry> Occluders => _occluders;

    /// <summary>
    /// Clears all state for the next frame.
    /// </summary>
    public void Clear()
    {
        _images.Clear();
        _occluders.Clear();
        _currentLayer = 0;
    }
}

/// <summary>
/// A KGP image registered during rendering, with its absolute position and layer.
/// </summary>
internal readonly record struct KgpImageEntry(
    KgpCellData Data,
    int AbsoluteX,
    int AbsoluteY,
    int Layer);

/// <summary>
/// A rectangular occluder (window bounds) registered during rendering.
/// </summary>
internal readonly record struct OccluderEntry(
    int Layer,
    Rect Bounds);
