using System.Globalization;
using System.Text;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Reconstructs Sixel graphics state for a freshly joining HMP1 peer by replaying
/// synthetic cursor-position and Sixel DCS escape sequences.
/// </summary>
/// <remarks>
/// <para>
/// Erase-display (<c>CSI 2 J</c>) unconditionally clears every Sixel placement on
/// the active screen (see <c>Hex1bTerminal.ApplyClearScreen</c>), and the
/// <see cref="Hmp1FrameType.StateSync"/> payload always begins with exactly that
/// sequence. This means Sixel replay must be queued <em>after</em> StateSync on the
/// wire — like <see cref="Hmp1KgpStateReplay"/> — or StateSync's own clear would
/// immediately erase it.
/// </para>
/// <para>
/// Unlike Kitty Graphics Protocol (KGP) placements, Sixel placements own the
/// character cells they occupy: creating a placement blanks its occupied cell
/// rectangle (see <c>Hex1bTerminal</c>'s cell-reservation behavior). Replaying
/// placement creation after StateSync therefore re-blanks any cell inside the
/// placement's rectangle that StateSync had just painted with real content —
/// including damaged (overwritten) cells. To compensate, this replay writes a
/// trailing "damage patch": for every cell the producer's placement recorded as
/// damaged, it re-emits that cell's exact character, colors, and attributes
/// (captured from the same snapshot the placements came from) immediately after
/// the placement-creation sequences, so the joining peer ends up with the same
/// end state as the producer: placement geometry intact, damaged cells showing
/// their overwritten content.
/// </para>
/// <para>
/// Rasterized placements are replayed by re-encoding their already-decoded
/// pixels (<see cref="SixelData.GetPixels"/>) into a fresh, self-contained
/// Sixel DCS sequence via <see cref="SixelExactEncoder"/>, rather than by
/// replaying the placement's original payload. This avoids a correctness gap:
/// the original payload may depend on persistent color registers set by
/// earlier, unreplayed images on the source terminal (<c>Hex1bTerminal</c>
/// keeps one <c>SixelColorRegisters</c> table for its whole lifetime), which a
/// freshly joining peer's terminal does not have. <see cref="SixelExactEncoder"/>
/// keeps the re-encode byte-exact, unlike <see cref="Hex1b.Surfaces.SixelEncoder"/>
/// (used for widget authoring), which intentionally quantizes to a reduced
/// palette and is not suitable here. Geometry-only placements have no decoded
/// pixels, so their original payload is replayed verbatim — safe because a
/// geometry-only outcome is a deterministic function of parser/raster policy
/// limits applied to the payload's own declared extents, not of any
/// persistent register state.
/// </para>
/// <para>
/// Only the viewport (not scrollback history) is replayed, matching the scope
/// of the existing KGP replay and the fact that <c>Hex1bTerminal.CreateSnapshot()</c>
/// (used for HMP1 state sync) captures zero scrollback lines by design.
/// </para>
/// </remarks>
internal static class Hmp1SixelStateReplay
{
    private const int TargetFrameSize = 1024 * 1024;

    /// <summary>
    /// Writes the escape sequences needed to recreate the given viewport Sixel
    /// placements — and repair any cells they damaged — on a fresh terminal,
    /// chunked into HMP1 <see cref="Hmp1FrameType.Output"/> frames.
    /// </summary>
    /// <param name="stream">The stream to write frames to.</param>
    /// <param name="placements">The producer's current viewport Sixel placements, captured from the same snapshot as <paramref name="damagedCells"/>.</param>
    /// <param name="damagedCells">Absolute (row, column, cell) triples for every cell any of <paramref name="placements"/> reports as damaged, captured from the same snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    internal static async Task WriteAsync(
        Stream stream,
        IReadOnlyList<SixelPlacement> placements,
        IReadOnlyList<(int Row, int Column, TerminalCell Cell)> damagedCells,
        CancellationToken ct)
    {
        if (placements.Count == 0)
            return;

        var frame = new StringBuilder(TargetFrameSize);

        foreach (var placement in placements.OrderBy(p => p.Sequence))
        {
            await AppendAsync(BuildPlacementSequence(placement)).ConfigureAwait(false);
        }

        foreach (var (row, column, cell) in damagedCells)
        {
            await AppendAsync(BuildDamagePatchSequence(row, column, cell)).ConfigureAwait(false);
        }

        await FlushAsync().ConfigureAwait(false);

        async ValueTask AppendAsync(string sequence)
        {
            if (frame.Length > 0 && frame.Length + sequence.Length > TargetFrameSize)
                await FlushAsync().ConfigureAwait(false);
            frame.Append(sequence);
        }

        async ValueTask FlushAsync()
        {
            if (frame.Length == 0)
                return;

            var payload = Encoding.UTF8.GetBytes(frame.ToString());
            frame.Clear();
            await Hmp1Protocol.WriteFrameAsync(
                stream,
                Hmp1FrameType.Output,
                payload,
                ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the cursor-position + Sixel DCS escape sequence needed to recreate a
    /// single placement on a fresh terminal.
    /// </summary>
    internal static string BuildPlacementSequence(SixelPlacement placement)
    {
        var payload = placement.IsGeometryOnly
            ? placement.Image.Payload
            : EncodeRasterizedPayload(placement.Image);

        return FormattableString.Invariant(
            $"\x1b[{placement.Row + 1};{placement.Column + 1}H") + payload;
    }

    /// <summary>
    /// Builds the cursor-position + SGR + character sequence needed to restore a
    /// single damaged cell's exact content after its owning placement has
    /// re-blanked it.
    /// </summary>
    private static string BuildDamagePatchSequence(int row, int column, TerminalCell cell)
    {
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant($"\x1b[{row + 1};{column + 1}H"));
        sb.Append("\x1b[0m");

        var attrs = cell.Attributes;
        var isReverse = (attrs & CellAttributes.Reverse) != 0;
        var fg = isReverse ? cell.Background : cell.Foreground;
        var bg = isReverse ? cell.Foreground : cell.Background;

        var sgrParts = new List<string>();
        if ((attrs & CellAttributes.Bold) != 0) sgrParts.Add("1");
        if ((attrs & CellAttributes.Dim) != 0) sgrParts.Add("2");
        if ((attrs & CellAttributes.Italic) != 0) sgrParts.Add("3");
        if ((attrs & CellAttributes.Underline) != 0) sgrParts.Add("4");
        if ((attrs & CellAttributes.Blink) != 0) sgrParts.Add("5");
        if ((attrs & CellAttributes.Hidden) != 0) sgrParts.Add("8");
        if ((attrs & CellAttributes.Strikethrough) != 0) sgrParts.Add("9");
        if ((attrs & CellAttributes.Overline) != 0) sgrParts.Add("53");
        if (fg.HasValue) sgrParts.Add(FormatColorSgr(fg.Value, isForeground: true));
        if (bg.HasValue) sgrParts.Add(FormatColorSgr(bg.Value, isForeground: false));

        if (sgrParts.Count > 0)
            sb.Append(FormattableString.Invariant($"\x1b[{string.Join(";", sgrParts)}m"));

        var ch = cell.Character;
        if (!string.IsNullOrEmpty(ch) && ch != "\0" && ch != "\uE000")
        {
            sb.Append((attrs & CellAttributes.Hidden) != 0
                ? new string(' ', DisplayWidth.GetGraphemeWidth(ch))
                : ch);
        }

        return sb.ToString();
    }

    private static string FormatColorSgr(Hex1bColor color, bool isForeground) => color.Kind switch
    {
        Hex1bColorKind.Standard => (isForeground ? 30 + color.AnsiIndex : 40 + color.AnsiIndex).ToString(CultureInfo.InvariantCulture),
        Hex1bColorKind.Bright => (isForeground ? 90 + color.AnsiIndex : 100 + color.AnsiIndex).ToString(CultureInfo.InvariantCulture),
        Hex1bColorKind.Indexed => FormattableString.Invariant($"{(isForeground ? "38;5" : "48;5")};{color.AnsiIndex}"),
        _ => FormattableString.Invariant($"{(isForeground ? "38;2" : "48;2")};{color.R};{color.G};{color.B}"),
    };


    private static string EncodeRasterizedPayload(SixelData image)
    {
        var pixels = image.GetPixels();

        // A non-geometry-only image should always have decoded pixels. Fall back to
        // the original payload defensively rather than dropping the placement.
        return pixels is null
            ? image.Payload
            : SixelExactEncoder.Encode(pixels) ?? image.Payload;
    }
}
