namespace KgpValidation.Scenarios;

/// <summary>
/// Exercises implemented animation frame storage without claiming playback or
/// composition support.
/// </summary>
/// <remarks>
/// The root frame is edited from red to cyan, a yellow second frame is appended,
/// and frame two is deleted. The final image is therefore cyan with one stored
/// frame. Playback-control and compose actions remain documented typed no-ops.
/// </remarks>
internal sealed class AnimationFrameScenario : KgpValidationScenario
{
    private const uint ImageId = 8201;

    public override string Id => "animation-frames";
    public override string Title => "Animation frame storage";
    public override string Area => "a=f append/edit and d=f frame deletion";
    public override string Expected =>
        "One cyan image. The red root was edited, a yellow frame was appended then deleted, leaving one frame.";
    public override string Protocol =>
        "a=T base; a=f,r=1 overwrites root; a=f appends frame 2; d=f,r=2 removes it. Playback is not claimed.";
    public override KgpScenarioExpectation ExpectedState { get; } =
        new(1, 1, FrameImageId: ImageId, FrameCount: 1);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.TextAt(
            9,
            8,
            "FINAL ROOT: CYAN    FRAME COUNT (HEADLESS CHECK): 1    PLAYBACK/COMPOSE: NOT IMPLEMENTED");
        writer.MoveTo(12, 10);
        writer.Kgp(
            $"a=T,f=32,s=48,v=48,i={ImageId},p=1,c=16,r=8,C=1,q=2",
            KgpImageFactory.CreateRgbaSolid(48, 48, 220, 45, 45));
        writer.Kgp(
            $"a=f,f=32,s=48,v=48,i={ImageId},r=1,X=1,z=200,q=2",
            KgpImageFactory.CreateRgbaSolid(48, 48, 20, 205, 220));
        writer.Kgp(
            $"a=f,f=32,s=48,v=48,i={ImageId},X=1,z=200,q=2",
            KgpImageFactory.CreateRgbaSolid(48, 48, 245, 210, 30));
        writer.Kgp($"a=d,d=f,i={ImageId},r=2,q=2");
    }
}
