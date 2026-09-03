using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Covers Hex1b's emitter direction: when Hex1b writes managed output that follows
/// a Sixel image, it must reposition the cursor explicitly instead of relying on
/// the upstream terminal's post-Sixel cursor behavior.
/// </summary>
/// <remarks>
/// This is the mirror image of <c>SixelCursorSemanticsTests</c>, which covers how
/// <c>Hex1bTerminal</c> interprets an incoming Sixel sequence.
/// </remarks>
[TestClass]
public class SixelEmitterCursorTests
{
    private const string Payload = "\x1bP7q#1;2;100;0;0#1~\x1b\\";

    private static SurfaceCell SixelCell(int widthInCells, int heightInCells) =>
        new(
            " ",
            null,
            null,
            Sixel: new TrackedObject<SixelData>(
                new SixelData(Payload, widthInCells, heightInCells, [1, 2, 3]),
                _ => { }));

    [TestMethod]
    public void ToTokens_TextAfterSixel_EmitsAnExplicitCursorPosition()
    {
        var previous = new Surface(10, 3);
        var current = new Surface(10, 3);
        current[0, 0] = SixelCell(2, 1);
        current[4, 0] = new SurfaceCell("X", null, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff, current).ToList();

        var sixelIndex = IndexOfSixel(tokens);
        var textIndex = IndexOf(tokens, token => token is TextToken text && text.Text.Contains('X'));
        Assert.IsGreaterThanOrEqualTo(0, sixelIndex);
        Assert.IsGreaterThan(sixelIndex, textIndex);
        Assert.IsTrue(
            tokens.Skip(sixelIndex + 1).Take(textIndex - sixelIndex - 1).OfType<CursorPositionToken>().Any(),
            "Managed output after a Sixel image must reposition the cursor explicitly.");
    }

    [TestMethod]
    public void ToTokens_SixelPlacement_IsPrecededByAnExplicitCursorPosition()
    {
        var previous = new Surface(10, 3);
        var current = new Surface(10, 3);
        current[3, 1] = SixelCell(2, 1);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff, current).ToList();

        var sixelIndex = IndexOfSixel(tokens);
        Assert.IsGreaterThanOrEqualTo(0, sixelIndex);

        var position = tokens.Take(sixelIndex).OfType<CursorPositionToken>().LastOrDefault();
        Assert.IsNotNull(position);
        Assert.AreEqual(2, position.Row);
        Assert.AreEqual(4, position.Column);
    }

    [TestMethod]
    public void SixelFragmentsToTokens_EveryFragment_IsPrecededByAnExplicitCursorPosition()
    {
        var surface = new Surface(10, 4);
        surface[1, 1] = SixelCell(2, 2);
        var composite = new CompositeSurface(10, 4);
        composite.AddLayer(surface, 0, 0);

        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is UnrecognizedSequenceToken raw && raw.Sequence.StartsWith("\x1bP", StringComparison.Ordinal))
            {
                Assert.IsGreaterThan(0, i);
                TestSeq.IsType<CursorPositionToken>(tokens[i - 1]);
            }
        }
    }

    private static int IndexOfSixel(IReadOnlyList<AnsiToken> tokens) => IndexOf(tokens, IsSixel);

    private static bool IsSixel(AnsiToken token) =>
        token is UnrecognizedSequenceToken raw &&
        raw.Sequence.StartsWith("\x1bP", StringComparison.Ordinal);

    private static int IndexOf(IReadOnlyList<AnsiToken> tokens, Func<AnsiToken, bool> predicate)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (predicate(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
