namespace KgpValidation.Scenarios;

/// <summary>
/// Makes negative and positive z-index behavior visible against ordinary text.
/// </summary>
internal sealed class ZOrderScenario : KgpValidationScenario
{
    public override string Id => "z-order";
    public override string Title => "Z-order and text occlusion";
    public override string Area => "Negative and positive z-index compositing";
    public override string Expected =>
        "Text remains readable over the large blue backplate; the smaller orange foreground covers the dotted guide.";
    public override string Protocol =>
        "Two a=T placements use z=-2 and z=2 around ordinary terminal cells.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(2, 2);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.TextAt(9, 5, "NEGATIVE Z BACKPLATE");
        writer.MoveTo(11, 6);
        writer.Kgp(
            "a=T,f=32,s=48,v=32,i=4201,p=1,c=42,r=8,z=-2,C=1,q=2",
            KgpImageFactory.CreateRgbaSolid(48, 32, 30, 90, 210));
        writer.TextAt(13, 10, "THIS TEXT MUST STAY ABOVE THE BLUE IMAGE");
        writer.TextAt(15, 10, "negative z-index -> graphics behind cells");

        writer.TextAt(18, 10, "positive z-index ->");
        writer.TextAt(19, 10, "........................................");
        writer.MoveTo(17, 31);
        writer.Kgp(
            "a=T,f=32,s=32,v=32,i=4202,p=1,c=14,r=6,z=2,C=1,q=2",
            KgpImageFactory.CreateRgbaSolid(32, 32, 245, 135, 30));
    }
}
