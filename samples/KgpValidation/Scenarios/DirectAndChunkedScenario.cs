using Hex1b;

namespace KgpValidation.Scenarios;

/// <summary>
/// Proves direct RGBA and multi-command chunked RGB uploads side by side.
/// </summary>
/// <remarks>
/// The chunk size is divisible by three so every non-final Base64 payload has a
/// valid boundary. A partial image on the right usually indicates upload-state
/// or continuation-control regression.
/// </remarks>
internal sealed class DirectAndChunkedScenario : KgpValidationScenario
{
    private const uint DirectImageId = 1201;
    private const uint ChunkedImageId = 1202;

    public override string Id => "direct-chunked";
    public override string Title => "Direct and chunked transfer";
    public override string Area => "a=T, a=t, f=24/32 and m=1/0 upload state";
    public override string Expected =>
        "Two clean images: a smooth RGBA gradient on the left and six sharp RGB color bars on the right.";
    public override string Protocol =>
        "Left is one direct transmit+display command; right is a 3072-byte chunk stream followed by a=p.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(2, 2);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        const int top = KgpScenarioLayout.GraphicsTop + 2;

        writer.TextAt(9, 4, "DIRECT RGBA");
        writer.MoveTo(top, 4);
        writer.Kgp(
            $"a=T,f=32,s=64,v=48,i={DirectImageId},p=1,c=16,r=9,C=1,q=2",
            KgpImageFactory.CreateRgbaGradient(64, 48));

        writer.TextAt(9, 43, "CHUNKED RGB (multiple m=1, final m=0)");
        writer.ChunkedTransmit(
            ChunkedImageId,
            width: 96,
            height: 54,
            KgpFormat.Rgb24,
            KgpImageFactory.CreateRgbBars(96, 54));
        writer.MoveTo(top, 43);
        writer.Kgp(
            $"a=p,i={ChunkedImageId},p=1,c=24,r=9,C=1,q=2");
    }
}
