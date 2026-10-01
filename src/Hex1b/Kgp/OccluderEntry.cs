using Hex1b.Layout;

namespace Hex1b.Kgp;

/// <summary>
/// A rectangular occluder (window bounds) registered during rendering.
/// </summary>
internal readonly record struct OccluderEntry(
    int Layer,
    Rect Bounds);
