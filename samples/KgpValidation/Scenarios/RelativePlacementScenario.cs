namespace KgpValidation.Scenarios;

/// <summary>
/// Builds a parent/child/grandchild graph, then replaces the root placement at
/// a new origin.
/// </summary>
/// <remarks>
/// Children use signed H/V offsets and are never independently repositioned.
/// They must follow the parent replacement as one graph, leaving the original
/// lower-right origin empty.
/// </remarks>
internal sealed class RelativePlacementScenario : KgpValidationScenario
{
    private const uint ParentImageId = 7201;
    private const uint ChildImageId = 7202;
    private const uint GrandchildImageId = 7203;

    public override string Id => "relative-placement";
    public override string Title => "Relative placement graph";
    public override string Area => "P/Q parent identity, signed H/V offsets and graph replacement";
    public override string Expected =>
        "A blue parent at FINAL with orange and green descendants offset from it; OLD ORIGIN contains no graphics.";
    public override string Protocol =>
        "Three stored images form a two-link relative graph; replacing the root (i,p) moves the graph as one unit.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(3, 3);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        Transmit(writer, ParentImageId, 35, 100, 220);
        Transmit(writer, ChildImageId, 245, 135, 25);
        Transmit(writer, GrandchildImageId, 40, 210, 95);

        writer.MoveTo(16, 52);
        writer.Kgp(
            $"a=p,i={ParentImageId},p=1,c=14,r=6,C=1,q=2");
        writer.Kgp(
            $"a=p,i={ChildImageId},p=2,c=7,r=3,P={ParentImageId},Q=1,H=10,V=4,C=1,q=2");
        writer.Kgp(
            $"a=p,i={GrandchildImageId},p=3,c=4,r=2,P={ChildImageId},Q=2,H=-2,V=-2,C=1,q=2");

        // Replacing only the root is the behavior under test.
        writer.MoveTo(11, 8);
        writer.Kgp(
            $"a=p,i={ParentImageId},p=1,c=14,r=6,C=1,q=2");

        writer.TextAt(9, 8, "FINAL ORIGIN");
        writer.TextAt(16, 52, "OLD ORIGIN - MUST BE CLEAR");
    }

    private static void Transmit(
        KgpProtocolWriter writer,
        uint imageId,
        byte red,
        byte green,
        byte blue)
    {
        writer.Kgp(
            $"a=t,f=32,s=32,v=32,i={imageId},q=2",
            KgpImageFactory.CreateRgbaSolid(32, 32, red, green, blue));
    }
}
