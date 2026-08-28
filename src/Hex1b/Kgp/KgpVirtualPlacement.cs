namespace Hex1b;

internal sealed record KgpVirtualPlacement(
    long GraphId,
    uint ImageId,
    uint PlacementId,
    uint Columns,
    uint Rows)
{
    internal long CreationOrdinal => GraphId;
}
