namespace KgpValidation.Scenarios;

/// <summary>
/// Displays one quadrant source image and four independently cropped views.
/// </summary>
internal sealed class SourceCropScenario : KgpValidationScenario
{
    private const uint ImageId = 3201;

    public override string Id => "source-crop";
    public override string Title => "Source rectangles and display geometry";
    public override string Area => "x/y/w/h source crop with c/r destination sizing";
    public override string Expected =>
        "A four-color reference on the left; four solid crops on the right ordered red, green, blue, yellow.";
    public override string Protocol =>
        "One a=t payload is reused by five a=p commands with explicit source rectangles and destination cells.";
    public override KgpScenarioExpectation ExpectedState { get; } = new(1, 5);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.Kgp(
            $"a=t,f=32,s=80,v=60,i={ImageId},q=2",
            KgpImageFactory.CreateRgbaQuadrants(80, 60));

        writer.TextAt(9, 3, "FULL REFERENCE");
        writer.MoveTo(11, 3);
        writer.Kgp(
            $"a=p,i={ImageId},p=1,c=16,r=8,C=1,q=2");

        writer.TextAt(9, 28, "CROPS: RED / GREEN, BLUE / YELLOW");
        Put(writer, 2, 11, 28, 8, 4, sourceX: 0, sourceY: 0);
        Put(writer, 3, 11, 40, 8, 4, sourceX: 40, sourceY: 0);
        Put(writer, 4, 17, 28, 8, 4, sourceX: 0, sourceY: 30);
        Put(writer, 5, 17, 40, 8, 4, sourceX: 40, sourceY: 30);
    }

    private static void Put(
        KgpProtocolWriter writer,
        uint placementId,
        int row,
        int column,
        int columns,
        int rows,
        int sourceX = 0,
        int sourceY = 0)
    {
        writer.MoveTo(row, column);
        writer.Kgp(
            $"a=p,i={ImageId},p={placementId},x={sourceX},y={sourceY}," +
            $"w=40,h=30,c={columns},r={rows},C=1,q=2");
    }
}
