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
        return RenderLinesWithLinks(inlines, maxWidth, baseForeground, baseAttributes).Lines.ToList();
    }

    /// <summary>
    /// Render inline AST elements into word-wrapped lines with embedded ANSI styling,
    /// returning both the rendered lines and link position metadata.
    /// </summary>
    public static WrapResult RenderLinesWithLinks(
        IReadOnlyList<MarkdownInline> inlines,
        int maxWidth,
        Hex1bColor? baseForeground = null,
        CellAttributes baseAttributes = CellAttributes.None,
        int focusedLinkId = -1,
        int hangingIndent = 0)
    {
        if (maxWidth <= 0)
            return new WrapResult([""], []);

        var runs = FlattenInlines(inlines, baseForeground, baseAttributes);
        var words = SplitIntoWords(runs);
        return WrapLinesWithLinks(words, maxWidth, focusedLinkId, hangingIndent);
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
        var linkIdCounter = 0;
        FlattenCore(inlines, baseForeground, null, baseAttributes, runs, ref linkIdCounter);
        return runs;
    }

    /// <summary>
    /// Extract link metadata from the inline AST without full flattening.
    /// Used during reconciliation to create link region nodes before layout.
    /// </summary>
    internal static List<(int LinkId, string Url, string Text)> ExtractLinks(
        IReadOnlyList<MarkdownInline> inlines)
    {
        var links = new List<(int, string, string)>();
        var linkIdCounter = 0;
        ExtractLinksCore(inlines, links, ref linkIdCounter);
        return links;
    }

    private static void ExtractLinksCore(
        IReadOnlyList<MarkdownInline> inlines,
        List<(int LinkId, string Url, string Text)> links,
        ref int linkIdCounter)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case EmphasisInline emphasis:
                    ExtractLinksCore(emphasis.Children, links, ref linkIdCounter);
                    break;
                case LinkInline link:
                    links.Add((linkIdCounter++, link.Url, link.Text));
                    break;
                case ImageInline image:
                    links.Add((linkIdCounter++, image.Url, $"[{image.AltText}]"));
                    break;
            }
        }
    }

    private static void FlattenCore(
        IReadOnlyList<MarkdownInline> inlines,
        Hex1bColor? fg,
        Hex1bColor? bg,
        CellAttributes attrs,
        List<MarkdownTextRun> runs,
        ref int linkIdCounter)
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
                    FlattenCore(emphasis.Children, fg, bg, emphasisAttrs, runs, ref linkIdCounter);
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
                    var linkId = linkIdCounter++;
                    runs.Add(new MarkdownTextRun(
                        link.Text,
                        linkFg,
                        bg,
                        attrs | CellAttributes.Underline,
                        link.Url,
                        linkId));
                    break;

                case ImageInline image:
                    // Render as [alt text] with link styling; URL for clickability
                    var imgFg = Hex1bColor.FromRgb(100, 160, 255);
                    var imgLinkId = linkIdCounter++;
                    runs.Add(new MarkdownTextRun(
                        $"[{image.AltText}]",
                        imgFg,
                        bg,
                        attrs | CellAttributes.Italic,
                        image.Url,
                        imgLinkId));
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

                currentFragments.Add(new MarkdownTextRun(segment, run.Foreground, run.Background, run.Attributes, run.Url, run.LinkId));
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
        return WrapLinesWithLinks(words, maxWidth).Lines.ToList();
    }

    /// <summary>
    /// Wrap styled words into lines, returning both rendered ANSI lines and
    /// link position metadata for each unique link.
    /// </summary>
    internal static WrapResult WrapLinesWithLinks(
        List<StyledWord> words, int maxWidth, int focusedLinkId = -1,
        int hangingIndent = 0)
    {
        var lines = new List<string>();
        var lineFragments = new List<MarkdownTextRun>();
        var currentX = 0;
        var lineIndex = 0;

        // Track the first position of each link (by LinkId)
        var linkFirstPositions = new Dictionary<int, (string Url, int LineIndex, int ColumnOffset)>();
        // Track total text per link
        var linkTexts = new Dictionary<int, List<string>>();
        // Track total display width per link
        var linkWidths = new Dictionary<int, int>();

        // First line uses full maxWidth; continuation lines are reduced by hangingIndent
        int LineWidth() => lineIndex == 0 ? maxWidth : Math.Max(1, maxWidth - hangingIndent);

        void TrackLinkFragments(IReadOnlyList<MarkdownTextRun> fragments, int wordStartX)
        {
            var fragmentX = wordStartX;
            foreach (var fragment in fragments)
            {
                var fragmentWidth = DisplayWidth.GetStringWidth(fragment.Text);
                if (fragment.Url != null && fragment.LinkId >= 0)
                {
                    if (!linkFirstPositions.ContainsKey(fragment.LinkId))
                    {
                        linkFirstPositions[fragment.LinkId] = (fragment.Url, lineIndex, fragmentX);
                        linkTexts[fragment.LinkId] = [];
                        linkWidths[fragment.LinkId] = 0;
                    }

                    linkTexts[fragment.LinkId].Add(fragment.Text);
                    linkWidths[fragment.LinkId] += fragmentWidth;
                }

                fragmentX += fragmentWidth;
            }
        }

        foreach (var word in words)
        {
            // Handle explicit line breaks
            if (word.Fragments.Count == 1 && word.Fragments[0].Text == "\n")
            {
                lines.Add(RenderFragmentsToAnsi(lineFragments, focusedLinkId));
                lineFragments = [];
                lineIndex++;
                // Continuation lines after a line break get hanging indent
                if (hangingIndent > 0 && lineIndex > 0)
                {
                    lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                    currentX = hangingIndent;
                }
                else
                {
                    currentX = 0;
                }
                continue;
            }

            var wordWidth = word.DisplayWidth;
            var spaceNeeded = (currentX > 0 && word.PrecededBySpace) ? 1 : 0;
            var lineWidth = LineWidth();

            if (wordWidth > lineWidth)
            {
                // Word is wider than the entire line — must character-break
                if (currentX > (lineIndex == 0 ? 0 : hangingIndent))
                {
                    lines.Add(RenderFragmentsToAnsi(lineFragments, focusedLinkId));
                    lineFragments = [];
                    lineIndex++;
                    if (hangingIndent > 0)
                    {
                        lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                        currentX = hangingIndent;
                    }
                    else
                    {
                        currentX = 0;
                    }
                }

                // Track link positions before character-breaking
                TrackLinkFragments(word.Fragments, currentX);

                // Build ANSI string for the word, then slice by display width
                var ansiWord = RenderFragmentsToAnsi(word.Fragments, focusedLinkId);
                var remaining = ansiWord;
                var remainingWidth = wordWidth;
                lineWidth = LineWidth();

                while (remainingWidth > lineWidth - (lineIndex == 0 ? 0 : 0))
                {
                    var sliceWidth = lineWidth - (currentX > 0 ? currentX : 0);
                    if (sliceWidth <= 0)
                    {
                        lines.Add(RenderFragmentsToAnsi(lineFragments, focusedLinkId));
                        lineFragments = [];
                        lineIndex++;
                        if (hangingIndent > 0)
                        {
                            lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                            currentX = hangingIndent;
                        }
                        else
                        {
                            currentX = 0;
                        }
                        lineWidth = LineWidth();
                        sliceWidth = lineWidth;
                    }
                    var (chunk, cols, _, _) = DisplayWidth.SliceByDisplayWidthWithAnsi(remaining, 0, sliceWidth);
                    if (lineFragments.Count > 0)
                    {
                        var prefix = RenderFragmentsToAnsi(lineFragments, focusedLinkId);
                        lines.Add(prefix + chunk);
                    }
                    else
                    {
                        lines.Add(chunk);
                    }
                    lineFragments = [];
                    remaining = remaining[chunk.Length..];
                    remainingWidth -= cols;
                    lineIndex++;
                    if (hangingIndent > 0)
                    {
                        lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                        currentX = hangingIndent;
                    }
                    else
                    {
                        currentX = 0;
                    }
                    lineWidth = LineWidth();
                }

                if (remaining.Length > 0)
                {
                    lineFragments.Add(new MarkdownTextRun(remaining, null, null, CellAttributes.None));
                    currentX += remainingWidth;
                }
            }
            else if (currentX + spaceNeeded + wordWidth > lineWidth)
            {
                // Word doesn't fit on current line.
                // If only the marker/indent is on this line, character-break the
                // word to avoid leaving the marker alone on a line.
                var onlyMarkerOnLine = hangingIndent > 0 && currentX <= hangingIndent;

                if (onlyMarkerOnLine)
                {
                    // Add space after marker if needed
                    if (spaceNeeded > 0)
                    {
                        lineFragments.Add(new MarkdownTextRun(" ", null, null, CellAttributes.None));
                        currentX += 1;
                    }

                    TrackLinkFragments(word.Fragments, currentX);

                    // Character-break: fill remaining space on current line
                    var ansiWord = RenderFragmentsToAnsi(word.Fragments, focusedLinkId);
                    var remaining = ansiWord;
                    var remainingWidth = wordWidth;

                    var sliceWidth = lineWidth - currentX;
                    if (sliceWidth > 0)
                    {
                        var (chunk, cols, _, _) = DisplayWidth.SliceByDisplayWidthWithAnsi(remaining, 0, sliceWidth);
                        var prefix = RenderFragmentsToAnsi(lineFragments, focusedLinkId);
                        lines.Add(prefix + chunk);
                        lineFragments = [];
                        remaining = remaining[chunk.Length..];
                        remainingWidth -= cols;
                        lineIndex++;
                    }

                    // Continue remainder on subsequent lines with hanging indent
                    var contWidth = Math.Max(1, maxWidth - hangingIndent);
                    while (remainingWidth > contWidth)
                    {
                        var (chunk2, cols2, _, _) = DisplayWidth.SliceByDisplayWidthWithAnsi(remaining, 0, contWidth);
                        lines.Add(new string(' ', hangingIndent) + chunk2);
                        remaining = remaining[chunk2.Length..];
                        remainingWidth -= cols2;
                        lineIndex++;
                    }

                    // Last piece goes into the current line fragments
                    if (remaining.Length > 0)
                    {
                        lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                        lineFragments.Add(new MarkdownTextRun(remaining, null, null, CellAttributes.None));
                        currentX = hangingIndent + remainingWidth;
                    }
                    else
                    {
                        lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                        currentX = hangingIndent;
                    }
                }
                else
                {
                    // Normal wrap — there's real content on this line already
                    lines.Add(RenderFragmentsToAnsi(lineFragments, focusedLinkId));
                    lineIndex++;
                    lineFragments = [];
                    if (hangingIndent > 0)
                    {
                        lineFragments.Add(new MarkdownTextRun(new string(' ', hangingIndent), null, null, CellAttributes.None));
                        lineFragments.AddRange(word.Fragments);
                        currentX = hangingIndent + wordWidth;
                        TrackLinkFragments(word.Fragments, hangingIndent);
                    }
                    else
                    {
                        lineFragments = [.. word.Fragments];
                        currentX = wordWidth;
                        TrackLinkFragments(word.Fragments, 0);
                    }
                }
            }
            else
            {
                // Word fits — append (with space if needed)
                var wordStartX = currentX;
                if (spaceNeeded > 0)
                {
                    lineFragments.Add(new MarkdownTextRun(" ", null, null, CellAttributes.None));
                    currentX += 1;
                    wordStartX = currentX;
                }

                lineFragments.AddRange(word.Fragments);
                TrackLinkFragments(word.Fragments, wordStartX);
                currentX += wordWidth;
            }
        }

        // Emit final line
        lines.Add(RenderFragmentsToAnsi(lineFragments, focusedLinkId));

        if (lines.Count == 0)
            lines.Add("");

        // Build link region info from tracked positions
        var linkRegions = new List<LinkRegionInfo>();
        foreach (var (linkId, (url, firstLine, firstCol)) in linkFirstPositions)
        {
            var fullText = string.Join(" ", linkTexts[linkId]);
            // Use the first fragment's width for the region (multi-word links
            // may have fragments on different lines; the first position is used
            // for focus scrolling)
            var firstFragmentWidth = linkWidths[linkId];
            linkRegions.Add(new LinkRegionInfo(
                linkId, url, fullText, firstLine, firstCol, firstFragmentWidth));
        }

        // Sort by LinkId to maintain document order
        linkRegions.Sort((a, b) => a.LinkId.CompareTo(b.LinkId));

        return new WrapResult(lines, linkRegions);
    }

    /// <summary>
    /// Render a list of styled fragments into a single ANSI-embedded string.
    /// Adjacent fragments with the same style are merged to reduce escape sequences.
    /// Fragments with URLs are wrapped in OSC 8 hyperlink sequences.
    /// When <paramref name="focusedLinkId"/> is set, fragments matching that link ID
    /// are rendered with reverse video for focus highlighting.
    /// </summary>
    internal static string RenderFragmentsToAnsi(
        IReadOnlyList<MarkdownTextRun> fragments, int focusedLinkId = -1)
    {
        if (fragments.Count == 0)
            return "";

        var sb = new StringBuilder();
        Hex1bColor? activeFg = null;
        Hex1bColor? activeBg = null;
        var activeAttrs = CellAttributes.None;
        string? activeUrl = null;

        foreach (var fragment in fragments)
        {
            if (fragment.Text.Length == 0)
                continue;

            // Determine effective styling — apply reverse video for focused link
            var effectiveFg = fragment.Foreground;
            var effectiveBg = fragment.Background;
            var effectiveAttrs = fragment.Attributes;
            if (focusedLinkId >= 0 && fragment.LinkId == focusedLinkId)
            {
                // Reverse video: swap fg/bg, add bold for visibility
                effectiveFg = fragment.Background ?? Hex1bColor.FromRgb(0, 0, 0);
                effectiveBg = fragment.Foreground ?? Hex1bColor.FromRgb(100, 160, 255);
                effectiveAttrs = effectiveAttrs | CellAttributes.Bold;
            }

            // Handle URL transitions (OSC 8 is independent of SGR)
            if (fragment.Url != activeUrl)
            {
                if (activeUrl != null)
                {
                    // End previous hyperlink
                    sb.Append("\x1b]8;;\x1b\\");
                }

                if (fragment.Url != null)
                {
                    // Start new hyperlink
                    sb.Append($"\x1b]8;;{fragment.Url}\x1b\\");
                }

                activeUrl = fragment.Url;
            }

            // Emit style changes
            EmitStyleTransition(sb, activeFg, activeBg, activeAttrs,
                effectiveFg, effectiveBg, effectiveAttrs);

            sb.Append(fragment.Text);

            activeFg = effectiveFg;
            activeBg = effectiveBg;
            activeAttrs = effectiveAttrs;
        }

        // End any active hyperlink
        if (activeUrl != null)
        {
            sb.Append("\x1b]8;;\x1b\\");
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
