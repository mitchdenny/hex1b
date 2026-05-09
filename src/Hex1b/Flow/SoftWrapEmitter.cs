using System.Globalization;
using System.Text;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Flow;

/// <summary>
/// Walks a <see cref="Surface"/> and emits its contents as soft-wrap-friendly
/// terminal output: per row, the visible characters with grouped SGR runs,
/// followed by <c>ESC[K</c> (clear-to-end-of-line) and <c>\n</c> (line feed).
/// </summary>
/// <remarks>
/// <para>
/// The legacy flow rendering path emits tombstones using absolute cursor
/// positioning (<c>ESC[r;cH&lt;text&gt;</c>) which writes characters into
/// specific terminal cells without producing soft-wrap state. When the host
/// terminal is later resized, those cells cannot be reflowed because the
/// terminal sees no logical line boundaries — the content disappears or
/// becomes corrupted.
/// </para>
/// <para>
/// <see cref="SoftWrapEmitter"/> renders content as proper logical lines so
/// the host terminal can:
/// <list type="bullet">
///   <item>soft-wrap rows that exceed the current width;</item>
///   <item>scroll older rows into the scrollback buffer naturally when new
///         rows are emitted at the bottom of the viewport;</item>
///   <item>preserve content across width and height resizes without any
///         intervention from Hex1b.</item>
/// </list>
/// </para>
/// <para>
/// Each row is emitted as <c>(SGR runs and characters) + ESC[K + \n</c>.
/// The terminating <c>ESC[K</c> clears any residual content past the rendered
/// glyphs, and the <c>\n</c> makes the row a hard logical line so subsequent
/// content cannot be confused with a wrapped continuation.
/// </para>
/// </remarks>
internal static class SoftWrapEmitter
{
    /// <summary>
    /// Emits the contents of <paramref name="surface"/> to the supplied
    /// adapter as soft-wrap-friendly text. The cursor is hidden for the
    /// duration of the emission and restored before the method returns.
    /// </summary>
    /// <param name="surface">The surface whose cells should be emitted.</param>
    /// <param name="adapter">
    /// The workload adapter receiving the bytes. Output is staged in a single
    /// buffer and flushed as one <see cref="IHex1bAppTerminalWorkloadAdapter.Write(string)"/>
    /// call to minimise tearing.
    /// </param>
    public static void Emit(Surface surface, IHex1bAppTerminalWorkloadAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(adapter);

        var sb = new StringBuilder(EstimateBufferSize(surface));

        // Hide cursor and force wraparound on while we paint. Wraparound is
        // on by default in every supported terminal, but we send DECAWM
        // explicitly so a host that previously toggled it off (e.g. via a
        // misbehaving sub-process) still gets the soft-wrap behaviour we
        // depend on. Cursor is restored at the end.
        sb.Append("\x1b[?25l");
        sb.Append("\x1b[?7h");

        for (int row = 0; row < surface.Height; row++)
        {
            EmitRow(surface, row, sb);
        }

        sb.Append("\x1b[?25h");

        adapter.Write(sb.ToString());
    }

    /// <summary>
    /// Builds the bytes that <see cref="Emit"/> would write, returning them
    /// instead of dispatching to an adapter. Used by tests.
    /// </summary>
    internal static string Format(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var sb = new StringBuilder(EstimateBufferSize(surface));
        sb.Append("\x1b[?25l");
        sb.Append("\x1b[?7h");
        for (int row = 0; row < surface.Height; row++)
        {
            EmitRow(surface, row, sb);
        }
        sb.Append("\x1b[?25h");
        return sb.ToString();
    }

    private static int EstimateBufferSize(Surface surface)
    {
        // Rough estimate: each cell averages ~2 chars (SGR overhead amortises
        // across runs), plus 4 bytes per row for ESC[K + \n, plus the cursor
        // and wraparound preamble.
        return surface.Width * surface.Height * 2 + surface.Height * 4 + 16;
    }

    private static void EmitRow(Surface surface, int row, StringBuilder sb)
    {
        // Track SGR state within the row. We always emit a leading reset
        // ("\x1b[0m") as part of the first SGR — see BuildSgrParameters where
        // stateUnknown=true forces a "0" — which guarantees the row starts
        // from a clean slate regardless of any prior styling left by the
        // caller (e.g. the active-step app).
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        var currentAttrs = CellAttributes.None;
        var currentUnderlineStyle = UnderlineStyle.None;
        Hex1bColor? currentUnderlineColor = null;
        bool stateUnknown = true;

        var lastContent = FindLastContentColumn(surface, row);

        // Emit cells from column 0 up to and including the last content
        // column. Anything past that is wiped with ESC[K below.
        int x = 0;
        while (x <= lastContent)
        {
            var cell = surface.GetCell(x, row);

            if (cell.IsContinuation)
            {
                // Wide-character continuation cells produce no output of
                // their own — the wide glyph in the previous cell already
                // covered this column.
                x++;
                continue;
            }

            var emit = cell;
            if (IsBlank(cell))
            {
                // Empty / unwritten cells render as a plain space with no
                // colour state so they don't accidentally extend a coloured
                // run from the previous cell.
                emit = SurfaceCells.Space(null, null);
            }

            var sgr = BuildSgrParameters(
                emit,
                stateUnknown,
                ref currentFg,
                ref currentBg,
                ref currentAttrs,
                ref currentUnderlineStyle,
                ref currentUnderlineColor);

            stateUnknown = false;

            if (sgr.Length > 0)
            {
                sb.Append("\x1b[");
                sb.Append(sgr);
                sb.Append('m');
            }

            sb.Append(emit.Character);

            x += Math.Max(1, emit.DisplayWidth);
        }

        // Reset SGR before the line clear so the cleared cells don't inherit
        // a coloured background from the last run on the row.
        if (!stateUnknown && (currentAttrs != CellAttributes.None
            || currentFg is not null
            || currentBg is not null))
        {
            sb.Append("\x1b[0m");
        }

        // Clear any residual content past the rendered glyphs (handles
        // overwriting the active-step region) and terminate the row as a
        // hard logical line.
        sb.Append("\x1b[K");
        sb.Append('\n');
    }

    private static int FindLastContentColumn(Surface surface, int row)
    {
        for (int x = surface.Width - 1; x >= 0; x--)
        {
            var cell = surface.GetCell(x, row);
            if (!IsBlank(cell) && !cell.IsContinuation)
            {
                return x;
            }
        }
        return -1;
    }

    private static bool IsBlank(SurfaceCell cell)
    {
        // Unwritten sentinel cell, or a cell that paints nothing visible.
        if (ReferenceEquals(cell.Character, SurfaceCells.UnwrittenMarker)
            || cell.Character == SurfaceCells.UnwrittenMarker)
        {
            return true;
        }

        if (string.IsNullOrEmpty(cell.Character))
        {
            return cell.Background is null;
        }

        return false;
    }

    // SGR parameter generation. Mirrors SurfaceComparer.BuildSgrParameters
    // intentionally — the surface comparer is the canonical implementation
    // for incremental SGR diffing during redraws, but it is private and
    // optimised for the diff path. Tombstone emission here is one-shot per
    // row, so we duplicate the small amount of logic rather than coupling
    // the two code paths.

    private static string BuildSgrParameters(
        SurfaceCell targetCell,
        bool stateUnknown,
        ref Hex1bColor? currentFg,
        ref Hex1bColor? currentBg,
        ref CellAttributes currentAttrs,
        ref UnderlineStyle currentUnderlineStyle,
        ref Hex1bColor? currentUnderlineColor)
    {
        var parts = new List<string>();

        var turnedOff = currentAttrs & ~targetCell.Attributes;
        bool needsReset = stateUnknown
            || turnedOff != CellAttributes.None
            || (currentFg is not null && targetCell.Foreground is null)
            || (currentBg is not null && targetCell.Background is null);

        if (needsReset)
        {
            parts.Add("0");
            currentAttrs = CellAttributes.None;
            currentFg = null;
            currentBg = null;
            currentUnderlineStyle = UnderlineStyle.None;
            currentUnderlineColor = null;
        }

        var toTurnOn = targetCell.Attributes & ~currentAttrs;

        if ((toTurnOn & CellAttributes.Bold) != 0)
            parts.Add("1");
        if ((toTurnOn & CellAttributes.Dim) != 0)
            parts.Add("2");
        if ((toTurnOn & CellAttributes.Italic) != 0)
            parts.Add("3");
        if ((toTurnOn & CellAttributes.Underline) != 0)
        {
            parts.Add(targetCell.UnderlineStyle switch
            {
                UnderlineStyle.Double => "21",
                UnderlineStyle.Curly => "4:3",
                UnderlineStyle.Dotted => "4:4",
                UnderlineStyle.Dashed => "4:5",
                _ => "4",
            });
        }
        else if ((currentAttrs & CellAttributes.Underline) != 0
            && (targetCell.Attributes & CellAttributes.Underline) != 0
            && targetCell.UnderlineStyle != currentUnderlineStyle)
        {
            parts.Add(targetCell.UnderlineStyle switch
            {
                UnderlineStyle.Double => "21",
                UnderlineStyle.Curly => "4:3",
                UnderlineStyle.Dotted => "4:4",
                UnderlineStyle.Dashed => "4:5",
                _ => "4",
            });
        }
        if ((toTurnOn & CellAttributes.Blink) != 0)
            parts.Add("5");
        if ((toTurnOn & CellAttributes.Reverse) != 0)
            parts.Add("7");
        if ((toTurnOn & CellAttributes.Hidden) != 0)
            parts.Add("8");
        if ((toTurnOn & CellAttributes.Strikethrough) != 0)
            parts.Add("9");
        if ((toTurnOn & CellAttributes.Overline) != 0)
            parts.Add("53");

        if (!ColorsEqual(targetCell.Foreground, currentFg) && targetCell.Foreground is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Foreground.Value, isForeground: true));
        }

        if (!ColorsEqual(targetCell.Background, currentBg) && targetCell.Background is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Background.Value, isForeground: false));
        }

        if (!ColorsEqual(targetCell.UnderlineColor, currentUnderlineColor))
        {
            if (targetCell.UnderlineColor is not null)
            {
                var ulc = targetCell.UnderlineColor.Value;
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"58;2;{ulc.R};{ulc.G};{ulc.B}"));
            }
            else if (currentUnderlineColor is not null)
            {
                parts.Add("59");
            }
        }

        currentAttrs = targetCell.Attributes;
        currentFg = targetCell.Foreground;
        currentBg = targetCell.Background;
        currentUnderlineStyle = targetCell.UnderlineStyle;
        currentUnderlineColor = targetCell.UnderlineColor;

        return string.Join(";", parts);
    }

    private static string BuildColorSgr(Hex1bColor color, bool isForeground)
    {
        return color.Kind switch
        {
            Hex1bColorKind.Standard => (isForeground ? 30 + color.AnsiIndex : 40 + color.AnsiIndex)
                .ToString(CultureInfo.InvariantCulture),
            Hex1bColorKind.Bright => (isForeground ? 90 + color.AnsiIndex : 100 + color.AnsiIndex)
                .ToString(CultureInfo.InvariantCulture),
            Hex1bColorKind.Indexed => string.Create(
                CultureInfo.InvariantCulture,
                $"{(isForeground ? "38;5" : "48;5")};{color.AnsiIndex}"),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"{(isForeground ? "38;2" : "48;2")};{color.R};{color.G};{color.B}"),
        };
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.R == b.Value.R
            && a.Value.G == b.Value.G
            && a.Value.B == b.Value.B
            && a.Value.Kind == b.Value.Kind
            && a.Value.AnsiIndex == b.Value.AnsiIndex;
    }
}
