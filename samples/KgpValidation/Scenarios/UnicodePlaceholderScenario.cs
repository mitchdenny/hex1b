using System.Text;

namespace KgpValidation.Scenarios;

/// <summary>
/// Realizes one virtual placement from a grid of official Unicode placeholder
/// graphemes.
/// </summary>
/// <remarks>
/// Foreground RGB encodes the image ID, underline RGB encodes the placement ID,
/// and the first two combining marks encode source row/column. Only the first
/// six official diacritics are needed for this 6x3 sample.
/// </remarks>
internal sealed class UnicodePlaceholderScenario : KgpValidationScenario
{
    private const uint ImageId = 6201;
    private const uint PlacementId = 7;
    private const int PlaceholderCodePoint = 0x10EEEE;

    private static readonly int[] Diacritics =
    [
        0x0305,
        0x030D,
        0x030E,
        0x0310,
        0x0312,
        0x033D,
    ];

    public override string Id => "unicode-placeholder";
    public override string Title => "Unicode placeholder placement";
    public override string Area => "U=1 virtual prototypes and U+10EEEE cell realization";
    public override string Expected =>
        "One continuous 6x3 gradient exactly covers the placeholder grid; no replacement glyphs or gaps are visible.";
    public override string Protocol =>
        "a=T,U=1 creates a prototype; colored U+10EEEE graphemes select image, placement, row and column.";
    public override KgpScenarioExpectation ExpectedState { get; } =
        new(1, 3, VirtualPlacementCount: 1);

    public override void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout)
    {
        writer.TextAt(9, 8, "The image below is positioned by Unicode cells, not an ordinary a=p placement.");
        writer.Kgp(
            $"a=T,U=1,f=32,s=60,v=60,i={ImageId},p={PlacementId},c=6,r=3,q=2",
            KgpImageFactory.CreateRgbaGradient(60, 60));

        var foreground = TrueColorSgr(38, ImageId);
        var underline = TrueColorSgr(58, PlacementId);
        for (var row = 0; row < 3; row++)
        {
            writer.MoveTo(12 + row, 12);
            writer.Raw(foreground);
            writer.Raw(underline);
            for (var column = 0; column < 6; column++)
                writer.Raw(Placeholder(row, column));
            writer.Raw("\x1b[0m");
        }
    }

    private static string TrueColorSgr(int selector, uint value)
        => $"\x1b[{selector};2;{(value >> 16) & 0xFF};" +
           $"{(value >> 8) & 0xFF};{value & 0xFF}m";

    private static string Placeholder(int row, int column)
        => new Rune(PlaceholderCodePoint).ToString() +
           new Rune(Diacritics[row]).ToString() +
           new Rune(Diacritics[column]).ToString();
}
