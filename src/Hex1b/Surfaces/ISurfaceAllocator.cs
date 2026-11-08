namespace Hex1b.Surfaces;

/// <summary>
/// Supplies reusable temporary surfaces without coupling rendering to a pooling implementation.
/// A null allocator selects ordinary surface allocation.
/// </summary>
internal interface ISurfaceAllocator
{
    Surface Rent(int width, int height, CellMetrics cellMetrics);
    void Return(Surface surface);
    void NextFrame();
}
