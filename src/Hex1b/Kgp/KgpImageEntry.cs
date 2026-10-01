using Hex1b.Layout;

namespace Hex1b.Kgp;

/// <summary>
/// A KGP image registered during rendering, with its absolute position and layer.
/// </summary>
internal readonly record struct KgpImageEntry(
    KgpCellData Data,
    int AbsoluteX,
    int AbsoluteY,
    int Layer);
