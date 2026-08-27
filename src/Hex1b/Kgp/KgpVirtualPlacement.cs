namespace Hex1b;

internal sealed record KgpVirtualPlacement(
    uint ImageId,
    uint PlacementId,
    uint Columns,
    uint Rows,
    long CreationOrdinal);
