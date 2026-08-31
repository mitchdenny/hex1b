namespace KgpValidation.Scenarios;

/// <summary>
/// Scrolls a text marker and ordinary image placement together inside margins.
/// </summary>
/// <remarks>
/// The final page is intentionally static: the protocol first creates the old
/// position and then scrolls three rows. A ghost at the lower position reveals
/// a placement-anchor or history-pruning regression.
/// </remarks>
internal sealed class ScrollingScenario : KgpValidationScenario
{
    private const uint ImageId = 5201;

    public override string Id => "scrolling";
    public override string Title => "Scrolling and placement anchors";
    public override string Area => "DECSTBM/CSI S geometry and history-aware placement movement";
    public override string Expected =>
        "Press R: the marker and checker move six rows upward together from OLD/BEFORE to EXPECTED AFTER.";
    public override string Protocol =>
        "An ordinary placement is created inside a scroll region, then CSI S runs six times before margins reset.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(1, 1);
    public override int VariantCount => 2;
    public override string ActionHint =>
        "R toggles BEFORE / AFTER. Watch the ANCHOR text and checker move together.";

    public override string GetExpected(int variant)
        => variant switch
        {
            0 => "BEFORE: marker and checker align with OLD/BEFORE. Press R to apply six real scroll operations.",
            1 => "AFTER: both align with EXPECTED AFTER. If only text moved, the presentation renderer failed.",
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
        => RenderVariant(writer, layout, variant: 0);

    public override void RenderVariant(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout,
        int variant)
    {
        if (variant is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(variant));

        var scrollBottom = layout.GraphicsBottom;
        writer.TextAt(
            9,
            4,
            variant == 0
                ? "STATE: BEFORE SCROLL - press R"
                : "STATE: AFTER SIX SCROLLS - press R to reset");
        writer.Raw($"\x1b[10;{scrollBottom}r");
        writer.TextAt(18, 9, "ANCHOR: this line moves with the image");
        writer.MoveTo(19, 9);
        writer.Kgp(
            $"a=T,f=32,s=48,v=32,i={ImageId},p=1,c=12,r=4,C=1,q=2",
            KgpImageFactory.CreateRgbaChecker(48, 32));
        if (variant == 1)
            writer.Raw("\x1b[S\x1b[S\x1b[S\x1b[S\x1b[S\x1b[S");
        writer.Raw("\x1b[r");
        writer.TextAt(13, 26, "< EXPECTED AFTER image top");
        writer.TextAt(
            19,
            26,
            variant == 0
                ? "< OLD/BEFORE image top"
                : "< OLD/BEFORE must now be empty");
        if (variant == 1)
        {
            writer.TextAt(
                21,
                26,
                "Text moved but image stayed? Presentation renderer: FAIL");
        }
    }
}
