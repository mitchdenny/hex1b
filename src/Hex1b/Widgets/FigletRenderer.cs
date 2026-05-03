using System.Text;

namespace Hex1b.Widgets;

/// <summary>
/// Internal FIGfont rendering engine. Composes input text into rendered ASCII-art lines
/// according to a font's smushing rules and the caller's chosen layout / overflow modes.
/// </summary>
internal static class FigletRenderer
{
    /// <summary>
    /// Renders <paramref name="text"/> into a list of output lines (one string per terminal row),
    /// with hardblanks already replaced by spaces.
    /// </summary>
    /// <param name="text">The input text. May contain newline characters.</param>
    /// <param name="font">The font to use.</param>
    /// <param name="horizontal">Horizontal layout mode, or <see cref="FigletLayoutMode.Default"/> to defer to the font.</param>
    /// <param name="vertical">Vertical layout mode, or <see cref="FigletLayoutMode.Default"/> to defer to the font.</param>
    /// <param name="horizontalOverflow">If <see cref="FigletHorizontalOverflow.Wrap"/>, wrap the input on whitespace at <paramref name="wrapWidth"/>.</param>
    /// <param name="wrapWidth">
    /// The wrap width in display columns. Ignored unless <paramref name="horizontalOverflow"/> is
    /// <see cref="FigletHorizontalOverflow.Wrap"/>; <see cref="int.MaxValue"/> means "do not wrap".
    /// </param>
    public static IReadOnlyList<string> Render(
        string text,
        FigletFont font,
        FigletLayoutMode horizontal,
        FigletLayoutMode vertical,
        FigletHorizontalOverflow horizontalOverflow,
        int wrapWidth)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        var hMode = ResolveHorizontal(horizontal, font);
        var vMode = ResolveVertical(vertical, font);

        // Split on explicit newlines first; each \n-segment is rendered separately and then
        // stacked vertically. We also track which paragraph blocks came from wrap-induced breaks
        // (vs. explicit \n) so we can use FullWidth vertical stacking between them — vertical
        // smushing across wrapped paragraphs causes adjacent paragraphs to bleed into each other,
        // which is almost never what the user wants.
        var paragraphs = text.Split('\n');

        var paragraphBlocks = new List<List<string>>(paragraphs.Length);
        var blockFromWrap = new List<bool>(paragraphs.Length);
        foreach (var paragraph in paragraphs)
        {
            if (horizontalOverflow == FigletHorizontalOverflow.Wrap && wrapWidth > 0 && wrapWidth != int.MaxValue)
            {
                var first = true;
                foreach (var wrappedLine in WrapParagraph(paragraph, font, hMode, wrapWidth))
                {
                    paragraphBlocks.Add(RenderHorizontalBlock(wrappedLine, font, hMode));
                    blockFromWrap.Add(!first);
                    first = false;
                }
            }
            else
            {
                paragraphBlocks.Add(RenderHorizontalBlock(paragraph, font, hMode));
                blockFromWrap.Add(false);
            }
        }

        // Stack paragraph blocks vertically. Wrap-induced paragraph breaks always stack with
        // FullWidth so wrapped lines remain visually distinct; explicit \n breaks use the
        // resolved vMode so callers can opt into vertical smushing/fitting where they want it.
        var combined = paragraphBlocks[0];
        for (var i = 1; i < paragraphBlocks.Count; i++)
        {
            var stackMode = blockFromWrap[i] ? FigletLayoutMode.FullWidth : vMode;
            combined = StackVertical(combined, paragraphBlocks[i], font, stackMode);
        }

        // Replace hardblanks with spaces for display. We also strip trailing spaces from each
        // line so callers' measurement (display width) reflects only meaningful content.
        var hardblank = font.Hardblank;
        var result = new string[combined.Count];
        for (var i = 0; i < combined.Count; i++)
        {
            var row = combined[i];
            if (row.IndexOf(hardblank) >= 0)
            {
                row = row.Replace(hardblank, ' ');
            }
            result[i] = row.TrimEnd(' ');
        }
        return result;
    }

    // ----- Layout resolution ---------------------------------------------------------------

    private static FigletLayoutMode ResolveHorizontal(FigletLayoutMode mode, FigletFont font)
    {
        if (mode != FigletLayoutMode.Default)
        {
            return mode;
        }
        if (font.HorizontalSmushing)
        {
            return FigletLayoutMode.Smushed;
        }
        if (font.HorizontalFitting)
        {
            return FigletLayoutMode.Fitted;
        }
        return FigletLayoutMode.FullWidth;
    }

    private static FigletLayoutMode ResolveVertical(FigletLayoutMode mode, FigletFont font)
    {
        if (mode != FigletLayoutMode.Default)
        {
            return mode;
        }
        if (font.VerticalSmushing)
        {
            return FigletLayoutMode.Smushed;
        }
        if (font.VerticalFitting)
        {
            return FigletLayoutMode.Fitted;
        }
        return FigletLayoutMode.FullWidth;
    }

    // ----- Horizontal block rendering ------------------------------------------------------

    private static List<string> RenderHorizontalBlock(string text, FigletFont font, FigletLayoutMode mode)
    {
        var height = font.Height;
        var rows = new StringBuilder[height];
        for (var i = 0; i < height; i++)
        {
            rows[i] = new StringBuilder();
        }
        var blockWidth = 0;

        var hasContent = false;
        var hardblank = font.Hardblank;
        var rules = font.HorizontalSmushingRules;

        // Iterate by Rune so surrogate-pair code points (e.g. emoji glyphs in custom fonts) work.
        foreach (var rune in text.EnumerateRunes())
        {
            var codePoint = rune.Value;
            var glyph = font.TryGetGlyph(codePoint, out var found) ? found : font.GetMissingGlyph();
            if (glyph.Width == 0)
            {
                continue;
            }

            if (!hasContent)
            {
                // First glyph: no merging.
                AppendGlyph(rows, glyph, height);
                blockWidth = glyph.Width;
                hasContent = true;
                continue;
            }

            var overlap = mode == FigletLayoutMode.FullWidth
                ? 0
                : ComputeHorizontalOverlap(rows, blockWidth, glyph, font.Height, mode, hardblank, rules);

            MergeGlyph(rows, blockWidth, glyph, overlap, font.Height, mode, hardblank, rules);
            blockWidth = blockWidth - overlap + glyph.Width;
        }

        var output = new List<string>(height);
        if (!hasContent)
        {
            // Empty input — produce `height` empty rows so vertical stacking works.
            for (var i = 0; i < height; i++)
            {
                output.Add(string.Empty);
            }
            return output;
        }

        for (var i = 0; i < height; i++)
        {
            output.Add(rows[i].ToString());
        }
        return output;
    }

    private static void AppendGlyph(StringBuilder[] rows, FigletGlyph glyph, int height)
    {
        var width = glyph.Width;
        for (var r = 0; r < height; r++)
        {
            var row = glyph.GetRow(r);
            rows[r].Append(row);
            // Pad short rows out to width with spaces so all rows have consistent length.
            if (row.Length < width)
            {
                rows[r].Append(' ', width - row.Length);
            }
        }
    }

    private static int ComputeHorizontalOverlap(
        StringBuilder[] rows,
        int blockWidth,
        FigletGlyph glyph,
        int height,
        FigletLayoutMode mode,
        char hardblank,
        int rules)
    {
        // Reference-figlet-style algorithm:
        //   1. fitted_amt = min over rows of (trailing-blanks-of-output[r] + leading-blanks-of-glyph[r])
        //   2. For Fitted mode, return fitted_amt.
        //   3. For Smushed mode, try fitted_amt + 1: every row must produce a successful merge at
        //      every cell of the new overlap region. If yes, return fitted_amt + 1; otherwise
        //      back off to fitted_amt. (Rows other than the "boundary row" trivially pass because
        //      they have extra blanks on at least one side; the boundary row is the one that
        //      pinned fitted_amt — there one visible-vs-visible cell needs the smush rules.)
        //
        // We deliberately do NOT search above fitted_amt + 1: monotonicity is not preserved at
        // larger overlaps (each step shifts every cell pair, not just adds one), and this matches
        // reference figlet's behavior so horizontal goldens stay byte-compatible.
        var maxPossible = Math.Min(blockWidth, glyph.Width);

        var fitted = int.MaxValue;
        for (var r = 0; r < height; r++)
        {
            var trailing = TrailingBlanks(rows[r], blockWidth, hardblank);
            var leading = LeadingBlanks(glyph.GetRow(r), hardblank);
            var perRow = trailing + leading;
            if (perRow < fitted)
            {
                fitted = perRow;
            }
        }

        if (fitted == int.MaxValue) fitted = 0;
        if (fitted > maxPossible) fitted = maxPossible;

        if (mode == FigletLayoutMode.Fitted)
        {
            return fitted;
        }

        // Smushed: try fitted + 1.
        var trial = fitted + 1;
        if (trial > maxPossible)
        {
            return fitted;
        }

        if (CanMergeAtOverlap(rows, blockWidth, glyph, trial, height, mode, hardblank, rules))
        {
            return trial;
        }
        return fitted;
    }

    private static int TrailingBlanks(StringBuilder row, int blockWidth, char hardblank)
    {
        // Count trailing space cells. Hardblanks count as VISIBLE (they protect their column),
        // matching reference figlet. blockWidth may exceed row.Length when prior rows extended
        // the buffer with spaces; treat past-end as blank.
        var i = blockWidth - 1;
        var n = 0;
        while (i >= 0)
        {
            var c = i < row.Length ? row[i] : ' ';
            if (c != ' ') break;
            n++;
            i--;
        }
        _ = hardblank;
        return n;
    }

    private static int LeadingBlanks(string row, char hardblank)
    {
        var n = 0;
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] != ' ') break;
            n++;
        }
        _ = hardblank;
        return n;
    }

    private static bool CanMergeAtOverlap(
        StringBuilder[] rows,
        int blockWidth,
        FigletGlyph glyph,
        int overlap,
        int height,
        FigletLayoutMode mode,
        char hardblank,
        int rules)
    {
        for (var r = 0; r < height; r++)
        {
            var glyphRow = glyph.GetRow(r);
            for (var i = 0; i < overlap; i++)
            {
                var leftIdx = blockWidth - overlap + i;
                var leftChar = leftIdx < rows[r].Length ? rows[r][leftIdx] : ' ';
                var rightChar = i < glyphRow.Length ? glyphRow[i] : ' ';

                if (!CanMergeCell(leftChar, rightChar, mode, hardblank, rules))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CanMergeCell(char left, char right, FigletLayoutMode mode, char hardblank, int rules)
    {
        if (left == ' ' || right == ' ')
        {
            return true;
        }
        if (mode == FigletLayoutMode.Fitted)
        {
            return false;
        }
        return FigletSmushingRules.TrySmushHorizontal(left, right, hardblank, rules, out _);
    }

    private static void MergeGlyph(
        StringBuilder[] rows,
        int blockWidth,
        FigletGlyph glyph,
        int overlap,
        int height,
        FigletLayoutMode mode,
        char hardblank,
        int rules)
    {
        for (var r = 0; r < height; r++)
        {
            var glyphRow = glyph.GetRow(r);
            var glyphWidth = glyph.Width;

            for (var i = 0; i < overlap; i++)
            {
                var leftIdx = blockWidth - overlap + i;
                var leftChar = leftIdx < rows[r].Length ? rows[r][leftIdx] : ' ';
                var rightChar = i < glyphRow.Length ? glyphRow[i] : ' ';

                var merged = MergeCell(leftChar, rightChar, mode, hardblank, rules);

                if (leftIdx < rows[r].Length)
                {
                    rows[r][leftIdx] = merged;
                }
                else
                {
                    if (rows[r].Length < leftIdx)
                    {
                        rows[r].Append(' ', leftIdx - rows[r].Length);
                    }
                    rows[r].Append(merged);
                }
            }

            for (var i = overlap; i < glyphWidth; i++)
            {
                rows[r].Append(i < glyphRow.Length ? glyphRow[i] : ' ');
            }
        }
    }

    private static char MergeCell(char left, char right, FigletLayoutMode mode, char hardblank, int rules)
    {
        if (left == ' ')
        {
            return right;
        }
        if (right == ' ')
        {
            return left;
        }
        if (mode == FigletLayoutMode.Fitted)
        {
            return left;
        }
        if (FigletSmushingRules.TrySmushHorizontal(left, right, hardblank, rules, out var resolved))
        {
            return resolved;
        }
        return left;
    }

    // ----- Vertical block stacking ---------------------------------------------------------

    private static List<string> StackVertical(
        List<string> upper,
        List<string> lower,
        FigletFont font,
        FigletLayoutMode mode)
    {
        if (upper.Count == 0)
        {
            return new List<string>(lower);
        }
        if (lower.Count == 0)
        {
            return new List<string>(upper);
        }

        // Pad both blocks to the same width with spaces so vertical merging is well-defined.
        var width = 0;
        for (var i = 0; i < upper.Count; i++) width = Math.Max(width, upper[i].Length);
        for (var i = 0; i < lower.Count; i++) width = Math.Max(width, lower[i].Length);

        var paddedUpper = PadRows(upper, width);
        var paddedLower = PadRows(lower, width);

        if (mode == FigletLayoutMode.FullWidth)
        {
            var result = new List<string>(paddedUpper.Count + paddedLower.Count);
            result.AddRange(paddedUpper);
            result.AddRange(paddedLower);
            return result;
        }

        var rules = font.VerticalSmushingRules;
        var hardblank = font.Hardblank;

        // Find max overlap (in rows) such that all column pairs in each overlap-row can merge.
        var maxTry = Math.Min(paddedUpper.Count, paddedLower.Count);
        var best = 0;
        for (var trial = 1; trial <= maxTry; trial++)
        {
            if (CanMergeRowsAt(paddedUpper, paddedLower, width, trial, mode, hardblank, rules))
            {
                best = trial;
            }
            else
            {
                break;
            }
        }

        if (best == 0)
        {
            // No fittable / smushable rows — just concatenate.
            var concat = new List<string>(paddedUpper.Count + paddedLower.Count);
            concat.AddRange(paddedUpper);
            concat.AddRange(paddedLower);
            return concat;
        }

        var merged = new List<string>(paddedUpper.Count + paddedLower.Count - best);

        // Upper rows above the overlap region, untouched.
        for (var i = 0; i < paddedUpper.Count - best; i++)
        {
            merged.Add(paddedUpper[i]);
        }

        // Merged rows.
        for (var i = 0; i < best; i++)
        {
            var u = paddedUpper[paddedUpper.Count - best + i];
            var l = paddedLower[i];
            merged.Add(MergeRow(u, l, mode, hardblank, rules));
        }

        // Lower rows below the overlap region.
        for (var i = best; i < paddedLower.Count; i++)
        {
            merged.Add(paddedLower[i]);
        }

        return merged;
    }

    private static List<string> PadRows(List<string> rows, int width)
    {
        var padded = new List<string>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            padded.Add(r.Length >= width ? r : r + new string(' ', width - r.Length));
        }
        return padded;
    }

    private static bool CanMergeRowsAt(
        List<string> upper,
        List<string> lower,
        int width,
        int overlap,
        FigletLayoutMode mode,
        char hardblank,
        int rules)
    {
        for (var i = 0; i < overlap; i++)
        {
            var u = upper[upper.Count - overlap + i];
            var l = lower[i];
            for (var c = 0; c < width; c++)
            {
                var uc = c < u.Length ? u[c] : ' ';
                var lc = c < l.Length ? l[c] : ' ';

                // Hardblanks act like blanks vertically.
                if (uc == hardblank) uc = ' ';
                if (lc == hardblank) lc = ' ';

                if (uc == ' ' || lc == ' ')
                {
                    continue;
                }

                if (mode == FigletLayoutMode.Fitted)
                {
                    return false;
                }

                if (!FigletSmushingRules.TrySmushVertical(uc, lc, hardblank, rules, out _))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static string MergeRow(string upper, string lower, FigletLayoutMode mode, char hardblank, int rules)
    {
        var width = Math.Max(upper.Length, lower.Length);
        var sb = new StringBuilder(width);
        for (var c = 0; c < width; c++)
        {
            var uc = c < upper.Length ? upper[c] : ' ';
            var lc = c < lower.Length ? lower[c] : ' ';

            // Hardblanks behave as blanks vertically.
            var uEffective = uc == hardblank ? ' ' : uc;
            var lEffective = lc == hardblank ? ' ' : lc;

            if (uEffective == ' ')
            {
                sb.Append(lc);
                continue;
            }
            if (lEffective == ' ')
            {
                sb.Append(uc);
                continue;
            }

            if (mode == FigletLayoutMode.Fitted)
            {
                // Should never reach here when CanMergeRowsAt guards.
                sb.Append(uc);
                continue;
            }

            if (FigletSmushingRules.TrySmushVertical(uEffective, lEffective, hardblank, rules, out var resolved))
            {
                sb.Append(resolved);
            }
            else
            {
                sb.Append(uc);
            }
        }
        return sb.ToString();
    }

    // ----- Word wrap -----------------------------------------------------------------------

    private static IEnumerable<string> WrapParagraph(string paragraph, FigletFont font, FigletLayoutMode hMode, int wrapWidth)
    {
        if (string.IsNullOrEmpty(paragraph))
        {
            yield return string.Empty;
            yield break;
        }

        var tokens = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var current = string.Empty;
        foreach (var rawToken in tokens)
        {
            // If a single token is wider than wrapWidth on its own, fall back to character-level
            // breaking so we never emit a row that exceeds the wrap target.
            var tokenChunks = MeasureRendered(rawToken, font, hMode) <= wrapWidth
                ? new[] { rawToken }
                : BreakTokenAtCharacters(rawToken, font, hMode, wrapWidth);

            foreach (var token in tokenChunks)
            {
                var candidate = current.Length == 0 ? token : current + ' ' + token;
                var width = MeasureRendered(candidate, font, hMode);

                if (current.Length == 0 || width <= wrapWidth)
                {
                    current = candidate;
                }
                else
                {
                    yield return current;
                    current = token;
                }
            }
        }

        if (current.Length > 0)
        {
            yield return current;
        }
    }

    /// <summary>
    /// Breaks a single overlong token into the largest character-prefix chunks that each fit
    /// within <paramref name="wrapWidth"/>. Used as a fallback when a single word is wider
    /// than the wrap target — e.g. very long URLs or German compound words.
    /// </summary>
    private static IEnumerable<string> BreakTokenAtCharacters(string token, FigletFont font, FigletLayoutMode hMode, int wrapWidth)
    {
        var start = 0;
        while (start < token.Length)
        {
            // Find the largest prefix length that fits. Always include at least one character so
            // we make progress even if a single FIGcharacter exceeds wrapWidth.
            var length = 1;
            while (start + length < token.Length)
            {
                var candidate = token.Substring(start, length + 1);
                if (MeasureRendered(candidate, font, hMode) > wrapWidth)
                {
                    break;
                }
                length++;
            }

            yield return token.Substring(start, length);
            start += length;
        }
    }

    private static int MeasureRendered(string text, FigletFont font, FigletLayoutMode hMode)
    {
        var rendered = RenderHorizontalBlock(text, font, hMode);
        return MaxRenderedWidth(rendered, font.Hardblank);
    }

    private static int MaxRenderedWidth(List<string> rendered, char hardblank)
    {
        var max = 0;
        for (var i = 0; i < rendered.Count; i++)
        {
            var row = rendered[i];
            // Trim trailing spaces (and hardblanks) the same way Render() will when emitting final lines.
            var len = row.Length;
            while (len > 0 && (row[len - 1] == ' ' || row[len - 1] == hardblank))
            {
                len--;
            }
            // Display width of the trimmed row, with hardblanks treated as spaces.
            var trimmed = len == row.Length ? row : row[..len];
            if (trimmed.IndexOf(hardblank) >= 0)
            {
                trimmed = trimmed.Replace(hardblank, ' ');
            }
            var w = DisplayWidth.GetStringWidth(trimmed);
            if (w > max) max = w;
        }
        return max;
    }
}
