namespace KgpValidation.Scenarios;

/// <summary>
/// Demonstrates placement-targeted deletion and reference-aware image lifetime.
/// </summary>
/// <remarks>
/// Lowercase deletion removes the left placement but retains reusable data. A
/// new right placement is then created without retransmission. Uppercase
/// deletion removes the middle placement but cannot reclaim the bytes while the
/// right placement still references them.
/// </remarks>
internal sealed class DeletionReuseScenario : KgpValidationScenario
{
    private const uint ImageId = 9201;

    public override string Id => "deletion-reuse";
    public override string Title => "Deletion and image lifetime";
    public override string Area => "d=i/I placement selectors and reference-aware reclamation";
    public override string Expected =>
        "Only the right bullseye remains. LEFT and MIDDLE are empty, and RIGHT proves the stored data stayed reusable.";
    public override string Protocol =>
        "Create p=1/2; delete p=1 lowercase; reuse bytes as p=3; delete p=2 uppercase while p=3 still owns data.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(1, 1);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.Kgp(
            $"a=t,f=32,s=48,v=48,i={ImageId},q=2",
            KgpImageFactory.CreateRgbaBullseye(48, 48));

        Put(writer, placementId: 1, column: 8);
        Put(writer, placementId: 2, column: 34);
        writer.Kgp($"a=d,d=i,i={ImageId},p=1,q=2");
        Put(writer, placementId: 3, column: 60);
        writer.Kgp($"a=d,d=I,i={ImageId},p=2,q=2");

        writer.TextAt(9, 8, "LEFT: empty");
        writer.TextAt(9, 34, "MIDDLE: empty");
        writer.TextAt(9, 60, "RIGHT: reused data");
    }

    private static void Put(
        KgpProtocolWriter writer,
        uint placementId,
        int column)
    {
        writer.MoveTo(12, column);
        writer.Kgp(
            $"a=p,i={ImageId},p={placementId},c=12,r=7,C=1,q=2");
    }
}
