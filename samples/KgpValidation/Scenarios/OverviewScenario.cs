namespace KgpValidation.Scenarios;

/// <summary>
/// Documents the current validation scope before any graphics are emitted.
/// </summary>
internal sealed class OverviewScenario : KgpValidationScenario
{
    public override string Id => "overview";
    public override string Title => "Compliance overview";
    public override string Area => "Harness scope and known limits";
    public override string Expected =>
        "This page is text-only. Read the supported areas, then press N or Space to begin the visual checks.";
    public override string Protocol =>
        "No KGP payload on this page; subsequent pages emit raw APC graphics commands through Hex1bTerminal.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(0, 0);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        string[] lines =
        [
            "[IMPLEMENTED] Direct RGB/RGBA transmission and chunked uploads",
            "[IMPLEMENTED] Image identity, replacement, reuse, crop, offsets and z-order",
            "[IMPLEMENTED] Scrolling/history anchors and per-screen graphics ownership",
            "[IMPLEMENTED] Unicode placeholders and relative placement graphs",
            "[IMPLEMENTED] Animation frame upload/edit/delete storage",
            "[IMPLEMENTED] Placement deletion selectors and reference-aware reclamation",
            "[PARTIAL]     Animation playback control and frame composition are typed no-ops",
            "",
            "Each following page describes one final image that can be checked by eye.",
            "Headless tests verify identity, geometry, z-order, crops, virtual state and frames.",
        ];

        for (var index = 0; index < lines.Length; index++)
        {
            writer.TextAt(
                KgpScenarioLayout.GraphicsTop + index,
                4,
                lines[index],
                Math.Max(1, layout.Width - 6));
        }
    }
}
