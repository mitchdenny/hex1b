namespace KgpValidation.Scenarios;

/// <summary>
/// Exercises one stored payload with multiple placements and atomic named
/// replacement of a second image/placement identity.
/// </summary>
/// <remarks>
/// The red replacement seed is intentionally placed at a different location
/// before the cyan retransmission. Any red image left behind is a stale
/// placement or renderer-index bug.
/// </remarks>
internal sealed class SharedAndReplacementScenario : KgpValidationScenario
{
    private const uint SharedImageId = 2201;
    private const uint ReplacementImageId = 2202;

    public override string Id => "shared-replacement";
    public override string Title => "Shared data and named replacement";
    public override string Area => "Stored image reuse, placement IDs and atomic replacement";
    public override string Expected =>
        "Three identical bullseyes at different sizes plus one cyan block at NEW; the OLD red slot is empty.";
    public override string Protocol =>
        "a=t once plus three a=p placements; same (i,p) is then retransmitted with new bytes and geometry.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(2, 4);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.TextAt(9, 4, "ONE PAYLOAD -> THREE PLACEMENTS");
        var target = KgpImageFactory.CreateRgbaBullseye(48, 48);
        writer.Kgp(
            $"a=t,f=32,s=48,v=48,i={SharedImageId},q=2",
            target);

        Put(writer, SharedImageId, placementId: 1, row: 11, column: 4, columns: 10, rows: 6);
        Put(writer, SharedImageId, placementId: 2, row: 11, column: 20, columns: 8, rows: 5);
        Put(writer, SharedImageId, placementId: 3, row: 11, column: 34, columns: 12, rows: 7);

        writer.TextAt(9, 58, "OLD (must be empty)");
        writer.MoveTo(11, 58);
        writer.Kgp(
            $"a=T,f=32,s=32,v=32,i={ReplacementImageId},p=7,c=10,r=5,C=1,q=2",
            KgpImageFactory.CreateRgbaSolid(32, 32, 220, 45, 45));

        writer.TextAt(17, 58, "NEW");
        writer.MoveTo(18, 58);
        writer.Kgp(
            $"a=T,f=32,s=32,v=32,i={ReplacementImageId},p=7,c=10,r=4,C=1,q=2",
            KgpImageFactory.CreateRgbaSolid(32, 32, 20, 210, 225));
    }

    private static void Put(
        KgpProtocolWriter writer,
        uint imageId,
        uint placementId,
        int row,
        int column,
        int columns,
        int rows)
    {
        writer.MoveTo(row, column);
        writer.Kgp(
            $"a=p,i={imageId},p={placementId},c={columns},r={rows},C=1,q=2");
    }
}
