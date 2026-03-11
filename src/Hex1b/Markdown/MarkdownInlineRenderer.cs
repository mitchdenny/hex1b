using System.Text;
using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// Transforms inline markdown AST elements into styled, word-wrapped lines
/// ready for terminal rendering. The pipeline is:
/// <list type="number">
///   <item>Flatten inline AST → <see cref="MarkdownTextRun"/> list (styled runs)</item>
///   <item>Split runs into <see cref="StyledWord"/> list (word-level wrapping units)</item>
///   <item>Wrap words into lines at a given width, emitting ANSI-embedded strings</item>
/// </list>
/// </summary>
internal static class MarkdownInlineRenderer
{
    /// <summary>
    /// Render inline AST elements into word-wrapped lines with embedded ANSI styling.
    /// </summary>
    /// <param name="inlines">The inline AST elements to render.</param>
    /// <param name="maxWidth">Maximum display width per line.</param>
    /// <param name="baseForeground">Optional base foreground color (e.g., heading color).</param>
    /// <param name="baseAttributes">Optional base attributes (e.g., Bold for headings).</param>
    /// <returns>A list of ANSI-embedded strings, one per wrapped line.</returns>
    public static List<string> RenderLines(
        IReadOnlyList<MarkdownInline> inlines,
        int maxWidth,
        Hex1bColor? baseForeground = null,
        CellAttributes baseAttributes = CellAttributes.None)
    {
        if (maxWidth <= 0)
            return [""];

        var runs = FlattenInlines(inlines, baseForeground, baseAttributes);
        var words = SplitIntoWords(runs);
        return WrapLines(words, maxWidth);
    }

    /// <summary>
    /// Flatten the inline AST into a list of styled runs. Nested emphasis composes
    /// attributes (e.g., bold wrapping italic = Bold|Italic).
    /// </summary>
    internal static List<MarkdownTextRun> FlattenInlines(
        IReadOnlyList<MarkdownInline> inlines,
        Hex1bColor? baseForeground = null,
        CellAttributes baseAttributes = CellAttributes.None)
    {
        var runs = new List<MarkdownTextRun>();
        FlattenCore(inlines, baseForeground, null, baseAttributes, runs);
        return runs;
    }

    private static void FlattenCore(
        IReadOnlyList<MarkdownInline> inlines,
        Hex1bColor? fg,
        Hex1bColor? bg,
        CellAttributes attrs,
        List<MarkdownTextRun> runs)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    if (text.Text.Length > 0)
                        runs.Add(new MarkdownTextRun(text.Text, fg, bg, attrs));
                    break;

                case EmphasisInline emphasis:
                    var emphasisAttrs = emphasis.IsStrong
                        ? attrs | CellAttributes.Bold
                        : attrs | CellAttributes.Italic;
                    FlattenCore(emphasis.Children, fg, bg, emphasisAttrs, runs);
                    break;

                case CodeInline code:
                    // Code spans get their own fg/bg but no word-splitting
                    runs.Add(new MarkdownTextRun(
                        code.Code,
                        Hex1bColor.FromRgb(220, 170, 120),  // InlineCodeForeground default
                        Hex1bColor.FromRgb(50, 50, 50),     // InlineCodeBackground default
                        attrs & ~(CellAttributes.Bold | CellAttributes.Italic)));
                    break;

                case LinkInline link:
                    // Links get colored + underlined text
                    var linkFg = Hex1bColor.FromRgb(100, 160, 255);  // LinkForeground default
                    runs.Add(new MarkdownTextRun(
                        link.Text,
                        linkFg,
                        bg,
                        attrs | CellAttributes.Underline));
                    break;

                case ImageInline image:
                    // Phase 2: render as [alt text] with link styling
                    var imgFg = Hex1bColor.FromRgb(100, 160, 255);
                    runs.Add(new MarkdownTextRun(
                        $"[{image.AltText}]",
                        imgFg,
                        bg,
                        attrs | CellAttributes.Italic));
                    break;

                case LineBreakInline lineBreak:
                    // Hard break = newline; soft break = space
                    runs.Add(new MarkdownTextRun(
                        lineBreak.IsHard ? "\n" : " ",
                        fg, bg, attrs));
                    break;
            }
        }
    }

    /// <summary>
    /// Split runs into styled words. Words are separated by spaces. A single word
    /// can span multiple runs (e.g., <c>par**tial**ly</c> is one word with 3 fragments).
    /// Code spans are never split on spaces — the entire code span is one word.
    /// </summary>
    internal static List<StyledWord> SplitIntoWords(List<MarkdownTextRun> runs)
    {
        var words = new List<StyledWord>();
        var currentFragments = new List<MarkdownTextRun>();
        var currentWidth = 0;
        var precededBySpace = false;

        foreach (var run in runs)
        {
            // Handle hard line breaks as word boundaries
            if (run.Text == "\n")
            {
                FlushWord(words, currentFragments, currentWidth, precededBySpace);
                currentFragments = [];
                currentWidth = 0;
                // Add a special newline word
                words.Add(new StyledWord([new MarkdownTextRun("\n", null, null, CellAttributes.None)], 0, false));
                precededBySpace = false;
                continue;
            }

            // Code spans are never split — treat as single atomic fragment
            if (run.Background != null)
            {
                // Check if this run has a background (code spans do) — keep it atomic
                if (run.Text.Length > 0)
                {
                    FlushWord(words, currentFragments, currentWidth, precededBySpace);
                    currentFragments = [];
                    currentWidth = 0;
                    words.Add(new StyledWord(
                        [run],
                        DisplayWidth.GetStringWidth(run.Text),
                        precededBySpace || HasLeadingSpace(run.Text)));
                    precededBySpace = HasTrailingSpace(run.Text);
                }
                continue;
            }

            // Split this run's text on spaces, creating word boundaries
            var text = run.Text;
            var i = 0;

            while (i < text.Length)
            {
                // Skip spaces
                if (text[i] == ' ')
                {
                    if (currentFragments.Count > 0 || currentWidth > 0)
                    {
                        FlushWord(words, currentFragments, currentWidth, precededBySpace);
                        currentFragments = [];
                        currentWidth = 0;
                    }
                    precededBySpace = true;
                    i++;
                    continue;
                }

                // Find the end of this word segment (up to next space or end of run)
                var wordStart = i;
                while (i < text.Length && text[i] != ' ')
                    i++;

                var segment = text[wordStart..i];
                var segmentWidth = DisplayWidth.GetStringWidth(segment);

                currentFragments.Add(new MarkdownTextRun(segment, run.Foreground, run.Background, run.Attributes));
                currentWidth += segmentWidth;
            }
        }

        // Flush any remaining word
        FlushWord(words, currentFragments, currentWidth, precededBySpace);

        return words;
    }

    private static void FlushWord(
        List<StyledWord> words,
        List<MarkdownTextRun> fragments,
        int width,
        bool precededBySpace)
    {
        if (fragments.Count > 0)
        {
            words.Add(new StyledWord(fragments.ToList(), width, precededBySpace));
        }
    }

    private static bool HasLeadingSpace(string text) => text.Length > 0 && text[0] == ' ';
    private static bool HasTrailingSpace(string text) => text.Length > 0 && text[^1] == ' ';

    /// <summary>
    /// Wrap styled words into lines at the given width. Each output line is an
    /// ANSI-embedded string ready for terminal rendering.
    /// </summary>
    internal static List<string> WrapLines(List<StyledWord> words, int maxWidth)
    {
        var lines = new List<string>();
        var lineFragments = new List<MarkdownTextRun>();
        var currentX = 0;

        foreach (var word in words)
        {
            // Handle explicit line breaks
            if (word.Fragments.Count == 1 && word.Fragments[0].Text == "\n")
            {
                lines.Add(RenderFragmentsToAnsi(lineFragments));
                lineFragments = [];
                currentX = 0;
                continue;
            }

            var wordWidth = word.DisplayWidth;
            var spaceNeeded = (currentX > 0 && word.PrecededBySpace) ? 1 : 0;

            if (wordWidth > maxWidth)
            {
                // Word is wider than the entire line — must character-break
                if (currentX > 0)
                {
                    lines.Add(RenderFragmentsToAnsi(lineFragments));
                    lineFragments = [];
                    currentX = 0;
                }

                // Build ANSI string for the word, then slice by display width
                var ansiWord = RenderFragmentsToAnsi(word.Fragments);
                var remaining = ansiWord;
                var remainingWidth = wordWidth;

                while (remainingWidth > maxWidth)
                {
                    var (chunk, cols, _, _) = DisplayWidth.SliceByDisplayWidthWithAnsi(remaining, 0, maxWidth);
                    lines.Add(chunk);
                    remaining = remaining[chunk.Length..];
                    remainingWidth -= cols;
                }

                if (remaining.Length > 0)
                {
                    // Parse the remaining back into a text run for fragment tracking
                    lineFragments = [new MarkdownTextRun(remaining, null, null, CellAttributes.None)];
                    currentX = remainingWidth;
                }
            }
            else if (currentX + spaceNeeded + wordWidth > maxWidth)
            {
                // Word doesn't fit on current line — wrap
                lines.Add(RenderFragmentsToAnsi(lineFragments));
                lineFragments = [.. word.Fragments];
                currentX = wordWidth;
            }
            else
            {
                // Word fits — append (with space if needed)
                if (spaceNeeded > 0)
                {
                    lineFragments.Add(new MarkdownTextRun(" ", null, null, CellAttributes.None));
                    currentX += 1;
                }

                lineFragments.AddRange(word.Fragments);
                currentX += wordWidth;
            }
        }

        // Emit final line
        lines.Add(RenderFragmentsToAnsi(lineFragments));

        return lines.Count > 0 ? lines : [""];
    }

    /// <summary>
    /// Render a list of styled fragments into a single ANSI-embedded string.
    /// Adjacent fragments with the same style are merged to reduce escape sequences.
    /// </summary>
    internal static string RenderFragmentsToAnsi(IReadOnlyList<MarkdownTextRun> fragments)
    {
        if (fragments.Count == 0)
            return "";

        var sb = new StringBuilder();
        Hex1bColor? activeFg = null;
        Hex1bColor? activeBg = null;
        var activeAttrs = CellAttributes.None;

        foreach (var fragment in fragments)
        {
            if (fragment.Text.Length == 0)
                continue;

            // Emit style changes
            EmitStyleTransition(sb, activeFg, activeBg, activeAttrs,
                fragment.Foreground, fragment.Background, fragment.Attributes);

            sb.Append(fragment.Text);

            activeFg = fragment.Foreground;
            activeBg = fragment.Background;
            activeAttrs = fragment.Attributes;
        }

        // Reset all active styles at end
        if (activeAttrs != CellAttributes.None || activeFg != null || activeBg != null)
        {
            sb.Append("\x1b[0m");
        }

        return sb.ToString();
    }

    private static void EmitStyleTransition(
        StringBuilder sb,
        Hex1bColor? fromFg, Hex1bColor? fromBg, CellAttributes fromAttrs,
        Hex1bColor? toFg, Hex1bColor? toBg, CellAttributes toAttrs)
    {
        // If styles are identical, no transition needed
        if (ColorsEqual(fromFg, toFg) && ColorsEqual(fromBg, toBg) && fromAttrs == toAttrs)
            return;

        // Reset everything first if we're changing attributes
        // (SGR codes are additive, easier to reset and re-apply)
        if (fromAttrs != CellAttributes.None || fromFg != null || fromBg != null)
        {
            sb.Append("\x1b[0m");
        }

        // Apply new attributes
        if ((toAttrs & CellAttributes.Bold) != 0)
            sb.Append("\x1b[1m");
        if ((toAttrs & CellAttributes.Dim) != 0)
            sb.Append("\x1b[2m");
        if ((toAttrs & CellAttributes.Italic) != 0)
            sb.Append("\x1b[3m");
        if ((toAttrs & CellAttributes.Underline) != 0)
            sb.Append("\x1b[4m");
        if ((toAttrs & CellAttributes.Strikethrough) != 0)
            sb.Append("\x1b[9m");

        // Apply new colors
        if (toFg != null && !toFg.Value.IsDefault)
            sb.Append(toFg.Value.ToForegroundAnsi());
        if (toBg != null && !toBg.Value.IsDefault)
            sb.Append(toBg.Value.ToBackgroundAnsi());
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Value.IsDefault == b.Value.IsDefault
            && a.Value.R == b.Value.R
            && a.Value.G == b.Value.G
            && a.Value.B == b.Value.B;
    }
}
