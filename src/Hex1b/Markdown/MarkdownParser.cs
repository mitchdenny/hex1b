using System.Text;

namespace Hex1b.Markdown;

/// <summary>
/// A lightweight markdown parser that produces a <see cref="MarkdownDocument"/> AST.
/// Supports headings, paragraphs, fenced/indented code blocks, block quotes,
/// ordered/unordered lists, and thematic breaks.
/// </summary>
public static class MarkdownParser
{
    /// <summary>
    /// Parse a markdown string into a document AST.
    /// </summary>
    public static MarkdownDocument Parse(string source)
    {
        if (string.IsNullOrEmpty(source))
            return new MarkdownDocument([]);

        var lines = SplitLines(source);
        var linkDefs = ExtractLinkDefinitions(lines);
        var blocks = ParseBlocks(lines, 0, lines.Count, linkDefs);
        return new MarkdownDocument(blocks, linkDefs);
    }

    /// <summary>
    /// Parse markdown from a <see cref="ReadOnlyMemory{T}"/> into a document AST.
    /// </summary>
    public static MarkdownDocument Parse(ReadOnlyMemory<char> source)
        => Parse(source.Span.ToString());

    private static List<string> SplitLines(string source)
    {
        var lines = new List<string>();
        int start = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                var end = i > 0 && source[i - 1] == '\r' ? i - 1 : i;
                lines.Add(source[start..end]);
                start = i + 1;
            }
        }

        if (start <= source.Length)
            lines.Add(source[start..]);

        return lines;
    }

    /// <summary>
    /// Extract link reference definitions ([label]: url "title") from the document.
    /// Definitions are removed from the line list so they don't appear as content.
    /// </summary>
    private static Dictionary<string, LinkDefinition> ExtractLinkDefinitions(List<string> lines)
    {
        var defs = new Dictionary<string, LinkDefinition>(StringComparer.OrdinalIgnoreCase);

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i].TrimStart();
            if (TryParseLinkDefinition(line, out var label, out var url, out var title))
            {
                // Iterating backwards, so later definitions are seen first.
                // Overwriting ensures the first (topmost) definition wins.
                defs[label] = new LinkDefinition(url, title);
                lines.RemoveAt(i);
            }
        }

        return defs;
    }

    private static bool TryParseLinkDefinition(
        string line, out string label, out string url, out string? title)
    {
        label = "";
        url = "";
        title = null;

        // Must start with [
        if (line.Length < 5 || line[0] != '[') return false;

        // Find closing ]
        var closeBracket = line.IndexOf(']', 1);
        if (closeBracket < 1) return false;

        // Must be followed by :
        if (closeBracket + 1 >= line.Length || line[closeBracket + 1] != ':')
            return false;

        label = line[1..closeBracket].Trim();
        if (label.Length == 0) return false;

        var rest = line[(closeBracket + 2)..].Trim();
        if (rest.Length == 0) return false;

        // Check for optional title in quotes
        // URL may be followed by "title", 'title', or (title)
        var quoteStart = -1;
        char quoteChar = '"';

        for (int i = 0; i < rest.Length; i++)
        {
            if (rest[i] == '"' || rest[i] == '\'')
            {
                quoteStart = i;
                quoteChar = rest[i];
                break;
            }
        }

        if (quoteStart > 0)
        {
            var quoteEnd = rest.LastIndexOf(quoteChar);
            if (quoteEnd > quoteStart)
            {
                title = rest[(quoteStart + 1)..quoteEnd];
                url = rest[..quoteStart].Trim();
            }
            else
            {
                url = rest;
            }
        }
        else
        {
            url = rest;
        }

        // Strip angle brackets from URL: <url>
        if (url.StartsWith('<') && url.EndsWith('>'))
            url = url[1..^1];

        return url.Length > 0;
    }

    private static List<MarkdownBlock> ParseBlocks(
        List<string> lines, int start, int end,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        var blocks = new List<MarkdownBlock>();
        int i = start;

        while (i < end)
        {
            // Skip blank lines
            if (IsBlankLine(lines[i]))
            {
                i++;
                continue;
            }

            // Try each block type in order of precedence
            if (TryParseThematicBreak(lines, i, out var thematicBreak))
            {
                blocks.Add(thematicBreak);
                i++;
                continue;
            }

            if (TryParseHeading(lines, i, out var heading, linkDefs))
            {
                blocks.Add(heading);
                i++;
                continue;
            }

            if (TryParseFencedCodeBlock(lines, i, end, out var fencedCode, out var fencedEnd))
            {
                blocks.Add(fencedCode);
                i = fencedEnd;
                continue;
            }

            if (TryParseBlockQuote(lines, i, end, out var blockQuote, out var bqEnd, linkDefs))
            {
                blocks.Add(blockQuote);
                i = bqEnd;
                continue;
            }

            if (TryParseTable(lines, i, end, out var table, out var tableEnd, linkDefs))
            {
                blocks.Add(table);
                i = tableEnd;
                continue;
            }

            if (TryParseList(lines, i, end, out var list, out var listEnd, linkDefs))
            {
                blocks.Add(list);
                i = listEnd;
                continue;
            }

            if (TryParseIndentedCodeBlock(lines, i, end, out var indentedCode, out var indentedEnd))
            {
                blocks.Add(indentedCode);
                i = indentedEnd;
                continue;
            }

            // Default: paragraph (consume until blank line or other block start)
            {
                var paraEnd = FindParagraphEnd(lines, i, end);
                var paraLines = new List<string>();
                for (int j = i; j < paraEnd; j++)
                    paraLines.Add(lines[j]);

                var text = string.Join(" ", paraLines.Select(l => l.Trim()));
                var inlines = ParseInlines(text, linkDefs);
                blocks.Add(new ParagraphBlock(inlines, FlattenInlinesToText(inlines)));
                i = paraEnd;
            }
        }

        return blocks;
    }

    // --- Thematic Break ---

    private static bool TryParseThematicBreak(List<string> lines, int index, out ThematicBreakBlock block)
    {
        block = null!;
        var line = lines[index].Trim();
        if (line.Length < 3) return false;

        char marker = line[0];
        if (marker != '-' && marker != '*' && marker != '_') return false;

        int count = 0;
        foreach (var c in line)
        {
            if (c == marker) count++;
            else if (c != ' ') return false;
        }

        if (count < 3) return false;

        block = new ThematicBreakBlock();
        return true;
    }

    // --- Heading ---

    private static bool TryParseHeading(
        List<string> lines, int index, out HeadingBlock heading,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        heading = null!;
        var line = lines[index];
        if (line.Length == 0 || line[0] != '#') return false;

        int level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
            level++;

        if (level == 0 || level > 6) return false;
        if (level < line.Length && line[level] != ' ') return false;

        var content = level < line.Length ? line[(level + 1)..].Trim() : "";

        // Remove optional closing #s
        if (content.Length > 0)
        {
            var trimEnd = content.Length - 1;
            while (trimEnd >= 0 && content[trimEnd] == '#')
                trimEnd--;
            if (trimEnd >= 0 && content[trimEnd] == ' ')
                content = content[..trimEnd].TrimEnd();
            else if (trimEnd < 0)
                content = "";
        }

        var inlines = ParseInlines(content, linkDefs);
        heading = new HeadingBlock(level, inlines, FlattenInlinesToText(inlines));
        return true;
    }

    // --- Fenced Code Block ---

    private static bool TryParseFencedCodeBlock(
        List<string> lines, int start, int end,
        out FencedCodeBlock codeBlock, out int blockEnd)
    {
        codeBlock = null!;
        blockEnd = start;
        var line = lines[start];
        var trimmed = line.TrimStart();
        var indent = line.Length - trimmed.Length;
        if (indent > 3) return false;

        char fence;
        int fenceLength;
        if (trimmed.Length >= 3 && trimmed[0] == '`' && !trimmed.Contains('`', 3))
        {
            fence = '`';
            fenceLength = CountLeading(trimmed, '`');
        }
        else if (trimmed.Length >= 3 && trimmed[0] == '~')
        {
            fence = '~';
            fenceLength = CountLeading(trimmed, '~');
        }
        else
        {
            return false;
        }

        if (fenceLength < 3) return false;

        // Parse info string (language + extra)
        var infoString = trimmed[fenceLength..].Trim();
        var language = "";
        var extra = "";
        if (infoString.Length > 0)
        {
            var spaceIdx = infoString.IndexOf(' ');
            if (spaceIdx >= 0)
            {
                language = infoString[..spaceIdx];
                extra = infoString[(spaceIdx + 1)..].Trim();
            }
            else
            {
                language = infoString;
            }
        }

        // Find closing fence
        var content = new StringBuilder();
        int i = start + 1;
        while (i < end)
        {
            var closeLine = lines[i].TrimStart();
            var closeIndent = lines[i].Length - closeLine.Length;
            if (closeIndent <= 3 &&
                closeLine.Length >= fenceLength &&
                CountLeading(closeLine, fence) >= fenceLength &&
                closeLine.Trim(fence).Trim().Length == 0)
            {
                i++;
                break;
            }

            // Remove up to 'indent' leading spaces from content lines
            var contentLine = RemoveIndent(lines[i], indent);
            if (content.Length > 0)
                content.Append('\n');
            content.Append(contentLine);
            i++;
        }

        codeBlock = new FencedCodeBlock(language, content.ToString(), extra);
        blockEnd = i;
        return true;
    }

    private static bool Contains(this string s, char c, int startIndex)
    {
        for (int i = startIndex; i < s.Length; i++)
            if (s[i] == c) return true;
        return false;
    }

    // --- Indented Code Block ---

    private static bool TryParseIndentedCodeBlock(
        List<string> lines, int start, int end,
        out IndentedCodeBlock codeBlock, out int blockEnd)
    {
        codeBlock = null!;
        blockEnd = start;

        if (!HasIndent(lines[start], 4)) return false;

        var content = new StringBuilder();
        int i = start;
        int lastNonBlank = start;

        while (i < end)
        {
            if (IsBlankLine(lines[i]))
            {
                content.Append('\n');
                i++;
                continue;
            }

            if (!HasIndent(lines[i], 4))
                break;

            if (content.Length > 0)
                content.Append('\n');
            content.Append(RemoveIndent(lines[i], 4));
            lastNonBlank = i;
            i++;
        }

        // Trim trailing blank lines from content
        var result = content.ToString().TrimEnd('\n');
        if (result.Length == 0) return false;

        codeBlock = new IndentedCodeBlock(result);
        blockEnd = lastNonBlank + 1;

        // Advance past any trailing blank lines consumed
        while (blockEnd < i)
            blockEnd++;

        return true;
    }

    // --- Block Quote ---

    private static bool TryParseBlockQuote(
        List<string> lines, int start, int end,
        out BlockQuoteBlock blockQuote, out int blockEnd,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        blockQuote = null!;
        blockEnd = start;

        if (!IsBlockQuoteLine(lines[start])) return false;

        var innerLines = new List<string>();
        int i = start;

        while (i < end)
        {
            if (IsBlockQuoteLine(lines[i]))
            {
                innerLines.Add(StripBlockQuotePrefix(lines[i]));
                i++;
            }
            else if (!IsBlankLine(lines[i]) && innerLines.Count > 0)
            {
                // Lazy continuation: non-blank line without > prefix
                // Only continue if it's not a block-level construct start
                if (IsBlockStart(lines[i]))
                    break;
                innerLines.Add(lines[i]);
                i++;
            }
            else
            {
                break;
            }
        }

        var children = ParseBlocks(innerLines, 0, innerLines.Count, linkDefs);
        blockQuote = new BlockQuoteBlock(children);
        blockEnd = i;
        return true;
    }

    private static bool IsBlockQuoteLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '>';
    }

    private static string StripBlockQuotePrefix(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '>')
        {
            var rest = trimmed[1..];
            if (rest.Length > 0 && rest[0] == ' ')
                return rest[1..];
            return rest;
        }

        return line;
    }

    // --- Tables ---

    private static bool TryParseTable(
        List<string> lines, int start, int end,
        out TableBlock table, out int tableEnd,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        table = null!;
        tableEnd = start;

        // Need at least 2 lines (header + delimiter)
        if (start + 1 >= end) return false;

        // First line must contain pipe
        var headerLine = lines[start].Trim();
        if (!headerLine.Contains('|')) return false;

        // Second line must be a valid delimiter row
        var delimLine = lines[start + 1].Trim();
        if (!TryParseTableDelimiterRow(delimLine, out var alignments)) return false;

        // Parse header cells
        var headerCells = ParseTableRow(headerLine, linkDefs);

        // Column count must match (pad or trim to delimiter count)
        var colCount = alignments.Count;
        while (headerCells.Count < colCount)
            headerCells.Add([]);
        if (headerCells.Count > colCount)
            headerCells = headerCells.Take(colCount).ToList();

        // Parse data rows
        var rows = new List<IReadOnlyList<IReadOnlyList<MarkdownInline>>>();
        int i = start + 2;
        while (i < end)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || !line.Contains('|'))
                break;

            var rowCells = ParseTableRow(line, linkDefs);
            // Pad or trim to column count
            while (rowCells.Count < colCount)
                rowCells.Add([]);
            if (rowCells.Count > colCount)
                rowCells = rowCells.Take(colCount).ToList();

            rows.Add(rowCells);
            i++;
        }

        table = new TableBlock(headerCells, alignments, rows);
        tableEnd = i;
        return true;
    }

    private static bool TryParseTableDelimiterRow(string line, out List<TableColumnAlignment> alignments)
    {
        alignments = null!;
        var cells = SplitTableRow(line);
        if (cells.Count == 0) return false;

        var result = new List<TableColumnAlignment>();
        foreach (var cell in cells)
        {
            var trimmed = cell.Trim();
            if (trimmed.Length == 0) return false;

            // Must be dashes with optional colons
            var leftColon = trimmed.StartsWith(':');
            var rightColon = trimmed.EndsWith(':');
            var inner = trimmed.TrimStart(':').TrimEnd(':');
            if (inner.Length == 0 || !inner.All(c => c == '-')) return false;

            if (leftColon && rightColon)
                result.Add(TableColumnAlignment.Center);
            else if (rightColon)
                result.Add(TableColumnAlignment.Right);
            else if (leftColon)
                result.Add(TableColumnAlignment.Left);
            else
                result.Add(TableColumnAlignment.None);
        }

        alignments = result;
        return true;
    }

    private static List<IReadOnlyList<MarkdownInline>> ParseTableRow(
        string line, Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        var cells = SplitTableRow(line);
        var result = new List<IReadOnlyList<MarkdownInline>>();
        foreach (var cell in cells)
        {
            var trimmed = cell.Trim();
            result.Add(ParseInlines(trimmed, linkDefs));
        }
        return result;
    }

    private static List<string> SplitTableRow(string line)
    {
        // Strip leading/trailing pipes
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
            trimmed = trimmed[1..];
        if (trimmed.EndsWith('|') && !trimmed.EndsWith("\\|"))
            trimmed = trimmed[..^1];

        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        bool escaped = false;
        bool inCode = false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (escaped)
            {
                current.Append(trimmed[i]);
                escaped = false;
                continue;
            }
            if (trimmed[i] == '\\')
            {
                escaped = true;
                continue;
            }
            if (trimmed[i] == '`')
            {
                inCode = !inCode;
                current.Append(trimmed[i]);
                continue;
            }
            if (trimmed[i] == '|' && !inCode)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(trimmed[i]);
        }
        cells.Add(current.ToString());

        return cells;
    }

    // --- Lists ---

    private static bool TryParseList(
        List<string> lines, int start, int end,
        out ListBlock list, out int listEnd,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        list = null!;
        listEnd = start;

        if (!TryGetListMarker(lines[start], out var isOrdered, out var startNumber, out var markerWidth))
            return false;

        var items = new List<ListItemBlock>();
        int i = start;

        while (i < end)
        {
            if (IsBlankLine(lines[i]))
            {
                // A blank line might separate list items or end the list
                if (i + 1 < end && TryGetListMarker(lines[i + 1], out var nextOrdered, out _, out _) &&
                    nextOrdered == isOrdered)
                {
                    i++;
                    continue;
                }

                break;
            }

            if (TryGetListMarker(lines[i], out var itemOrdered, out _, out var itemMarkerWidth) &&
                itemOrdered == isOrdered)
            {
                // Start of a new list item
                var itemLines = new List<string>();
                var contentPart = lines[i][itemMarkerWidth..];
                itemLines.Add(contentPart);
                i++;

                // Continuation lines: indented by at least markerWidth or blank
                while (i < end)
                {
                    if (IsBlankLine(lines[i]))
                    {
                        // Blank line within list item
                        if (i + 1 < end && HasIndent(lines[i + 1], itemMarkerWidth))
                        {
                            itemLines.Add("");
                            i++;
                            continue;
                        }

                        break;
                    }

                    if (HasIndent(lines[i], itemMarkerWidth))
                    {
                        itemLines.Add(RemoveIndent(lines[i], itemMarkerWidth));
                        i++;
                    }
                    else if (TryGetListMarker(lines[i], out var nextItemOrdered, out _, out _) &&
                             nextItemOrdered == isOrdered)
                    {
                        break;
                    }
                    else if (!IsBlockStart(lines[i]))
                    {
                        // Lazy continuation
                        itemLines.Add(lines[i]);
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                var children = ParseBlocks(itemLines, 0, itemLines.Count, linkDefs);
                var isChecked = ExtractTaskListCheckbox(children);
                items.Add(new ListItemBlock(children, isChecked));
            }
            else
            {
                break;
            }
        }

        if (items.Count == 0) return false;

        list = new ListBlock(isOrdered, startNumber, items);
        listEnd = i;
        return true;
    }

    private static bool TryGetListMarker(
        string line, out bool isOrdered, out int startNumber, out int markerWidth)
    {
        isOrdered = false;
        startNumber = 1;
        markerWidth = 0;

        var trimmed = line.TrimStart();
        var indent = line.Length - trimmed.Length;
        if (indent > 3 || trimmed.Length == 0) return false;

        // Unordered: -, *, +
        if (trimmed.Length >= 2 && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') &&
            trimmed[1] == ' ')
        {
            // Make sure '-' isn't a thematic break
            if (trimmed[0] == '-' && trimmed.Trim().All(c => c == '-' || c == ' ') && trimmed.Count(c => c == '-') >= 3)
                return false;

            isOrdered = false;
            markerWidth = indent + 2;
            return true;
        }

        // Ordered: 1. or 1)
        int numEnd = 0;
        while (numEnd < trimmed.Length && char.IsAsciiDigit(trimmed[numEnd]))
            numEnd++;

        if (numEnd > 0 && numEnd <= 9 && numEnd < trimmed.Length &&
            (trimmed[numEnd] == '.' || trimmed[numEnd] == ')') &&
            numEnd + 1 < trimmed.Length && trimmed[numEnd + 1] == ' ')
        {
            isOrdered = true;
            startNumber = int.Parse(trimmed[..numEnd]);
            markerWidth = indent + numEnd + 2; // digits + delimiter + space
            return true;
        }

        return false;
    }

    // --- Paragraph ---

    private static int FindParagraphEnd(List<string> lines, int start, int end)
    {
        int i = start;
        while (i < end)
        {
            if (IsBlankLine(lines[i])) break;
            if (i > start && IsBlockStart(lines[i])) break;
            i++;
        }

        return i;
    }

    private static bool IsBlockStart(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return false;

        // Heading
        if (trimmed[0] == '#') return true;

        // Fenced code
        if (trimmed.Length >= 3 && (trimmed[0] == '`' || trimmed[0] == '~'))
        {
            var c = trimmed[0];
            if (CountLeading(trimmed, c) >= 3) return true;
        }

        // Block quote
        if (trimmed[0] == '>') return true;

        // Thematic break
        if (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '_')
        {
            if (TryParseThematicBreak([line], 0, out _)) return true;
        }

        // List marker
        if (TryGetListMarker(line, out _, out _, out _)) return true;

        return false;
    }

    // --- Inline Parsing ---

    internal static List<MarkdownInline> ParseInlines(
        string text, Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        var inlines = new List<MarkdownInline>();
        if (string.IsNullOrEmpty(text))
            return inlines;

        ParseInlinesCore(text.AsSpan(), inlines, linkDefs);
        return inlines;
    }

    private static void ParseInlinesCore(
        ReadOnlySpan<char> text, List<MarkdownInline> result,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        int i = 0;
        int textStart = 0;

        while (i < text.Length)
        {
            // Code span: `code`
            if (text[i] == '`')
            {
                FlushText(text, textStart, i, result);

                var backtickCount = CountLeadingSpan(text[i..], '`');
                var contentStart = i + backtickCount;
                var closeIdx = FindClosingBackticks(text, contentStart, backtickCount);

                if (closeIdx >= 0)
                {
                    var code = text[contentStart..closeIdx].ToString().Trim();
                    result.Add(new CodeInline(code));
                    i = closeIdx + backtickCount;
                    textStart = i;
                    continue;
                }

                // No closing backticks — treat as literal text
                i += backtickCount;
                continue;
            }

            // Emphasis: ** or * or __ or _
            if ((text[i] == '*' || text[i] == '_') && i + 1 < text.Length)
            {
                var marker = text[i];
                var count = CountLeadingSpan(text[i..], marker);

                if (count >= 2 && TryParseEmphasis(text, i, marker, 2, out var strongContent, out var strongEnd))
                {
                    FlushText(text, textStart, i, result);
                    var children = new List<MarkdownInline>();
                    ParseInlinesCore(strongContent, children, linkDefs);
                    result.Add(new EmphasisInline(true, children));
                    i = strongEnd;
                    textStart = i;
                    continue;
                }

                if (count >= 1 && TryParseEmphasis(text, i, marker, 1, out var emContent, out var emEnd))
                {
                    FlushText(text, textStart, i, result);
                    var children = new List<MarkdownInline>();
                    ParseInlinesCore(emContent, children, linkDefs);
                    result.Add(new EmphasisInline(false, children));
                    i = emEnd;
                    textStart = i;
                    continue;
                }

                i++;
                continue;
            }

            // Strikethrough: ~~text~~
            if (text[i] == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                if (TryParseEmphasis(text, i, '~', 2, out var strikeContent, out var strikeEnd))
                {
                    FlushText(text, textStart, i, result);
                    var children = new List<MarkdownInline>();
                    ParseInlinesCore(strikeContent, children, linkDefs);
                    result.Add(new StrikethroughInline(children));
                    i = strikeEnd;
                    textStart = i;
                    continue;
                }

                i++;
                continue;
            }

            // Link: [text](url), [text][ref], or [text][]
            if (text[i] == '[')
            {
                if (TryParseLink(text, i, out var link, out var linkEnd, linkDefs))
                {
                    FlushText(text, textStart, i, result);
                    result.Add(link);
                    i = linkEnd;
                    textStart = i;
                    continue;
                }

                i++;
                continue;
            }

            // Image: ![alt](url)
            if (text[i] == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (TryParseImage(text, i, out var image, out var imgEnd, linkDefs))
                {
                    FlushText(text, textStart, i, result);
                    result.Add(image);
                    i = imgEnd;
                    textStart = i;
                    continue;
                }

                i++;
                continue;
            }

            // Hard line break: two trailing spaces before newline, or backslash before newline
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                FlushText(text, textStart, i, result);
                result.Add(new LineBreakInline(true));
                i += 2;
                textStart = i;
                continue;
            }

            if (text[i] == '\n')
            {
                // Check for hard break (two spaces before newline)
                var isHard = i >= 2 && text[i - 1] == ' ' && text[i - 2] == ' ';
                FlushText(text, textStart, isHard ? i - 2 : i, result);
                result.Add(new LineBreakInline(isHard));
                i++;
                textStart = i;
                continue;
            }

            i++;
        }

        FlushText(text, textStart, text.Length, result);
    }

    private static void FlushText(ReadOnlySpan<char> text, int start, int end, List<MarkdownInline> result)
    {
        if (end > start)
        {
            result.Add(new TextInline(text[start..end].ToString()));
        }
    }

    private static bool TryParseEmphasis(
        ReadOnlySpan<char> text, int start, char marker, int markerCount,
        out ReadOnlySpan<char> content, out int end)
    {
        content = default;
        end = start;

        var openEnd = start + markerCount;
        if (openEnd >= text.Length) return false;

        // Find matching closing markers
        for (int i = openEnd; i <= text.Length - markerCount; i++)
        {
            if (text[i] == marker && CountLeadingSpan(text[i..], marker) >= markerCount)
            {
                content = text[openEnd..i];
                end = i + markerCount;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseLink(
        ReadOnlySpan<char> text, int start,
        out LinkInline link, out int end,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        link = null!;
        end = start;

        var closeBracket = FindUnescaped(text, start + 1, ']');
        if (closeBracket < 0) return false;

        var linkText = text[(start + 1)..closeBracket].ToString();

        // [text](url) or [text](url "title")
        if (closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
        {
            var closeParen = FindUnescaped(text, closeBracket + 2, ')');
            if (closeParen < 0) return false;

            var urlPart = text[(closeBracket + 2)..closeParen].ToString().Trim();

            string? title = null;
            var quoteStart = urlPart.IndexOf('"');
            if (quoteStart >= 0)
            {
                var quoteEnd = urlPart.LastIndexOf('"');
                if (quoteEnd > quoteStart)
                {
                    title = urlPart[(quoteStart + 1)..quoteEnd];
                    urlPart = urlPart[..quoteStart].Trim();
                }
            }

            link = new LinkInline(linkText, urlPart, title);
            end = closeParen + 1;
            return true;
        }

        // Reference-style: [text][ref] or [text][]
        if (linkDefs != null && closeBracket + 1 < text.Length && text[closeBracket + 1] == '[')
        {
            var refClose = FindUnescaped(text, closeBracket + 2, ']');
            if (refClose >= 0)
            {
                var refLabel = text[(closeBracket + 2)..refClose].ToString().Trim();
                // [text][] means ref = text
                if (refLabel.Length == 0)
                    refLabel = linkText;

                if (linkDefs.TryGetValue(refLabel, out var def))
                {
                    link = new LinkInline(linkText, def.Url, def.Title);
                    end = refClose + 1;
                    return true;
                }
            }
        }

        // Shortcut reference: [text] (no following brackets/parens)
        if (linkDefs != null && linkDefs.TryGetValue(linkText, out var shortcutDef))
        {
            // Only match if next char is NOT [ or ( — those are handled above
            if (closeBracket + 1 >= text.Length ||
                (text[closeBracket + 1] != '(' && text[closeBracket + 1] != '['))
            {
                link = new LinkInline(linkText, shortcutDef.Url, shortcutDef.Title);
                end = closeBracket + 1;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseImage(
        ReadOnlySpan<char> text, int start,
        out ImageInline image, out int end,
        Dictionary<string, LinkDefinition>? linkDefs = null)
    {
        image = null!;
        end = start;

        // ![alt]...
        if (start + 1 >= text.Length || text[start + 1] != '[')
            return false;

        var closeBracket = FindUnescaped(text, start + 2, ']');
        if (closeBracket < 0) return false;

        var altText = text[(start + 2)..closeBracket].ToString();

        // ![alt](url)
        if (closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
        {
            var closeParen = FindUnescaped(text, closeBracket + 2, ')');
            if (closeParen < 0) return false;

            var urlPart = text[(closeBracket + 2)..closeParen].ToString().Trim();

            string? title = null;
            var quoteStart = urlPart.IndexOf('"');
            if (quoteStart >= 0)
            {
                var quoteEnd = urlPart.LastIndexOf('"');
                if (quoteEnd > quoteStart)
                {
                    title = urlPart[(quoteStart + 1)..quoteEnd];
                    urlPart = urlPart[..quoteStart].Trim();
                }
            }

            image = new ImageInline(altText, urlPart, title);
            end = closeParen + 1;
            return true;
        }

        // ![alt][ref] or ![alt][]
        if (linkDefs != null && closeBracket + 1 < text.Length && text[closeBracket + 1] == '[')
        {
            var refClose = FindUnescaped(text, closeBracket + 2, ']');
            if (refClose >= 0)
            {
                var refLabel = text[(closeBracket + 2)..refClose].ToString().Trim();
                if (refLabel.Length == 0)
                    refLabel = altText;

                if (linkDefs.TryGetValue(refLabel, out var def))
                {
                    image = new ImageInline(altText, def.Url, def.Title);
                    end = refClose + 1;
                    return true;
                }
            }
        }

        return false;
    }

    private static int FindClosingBackticks(ReadOnlySpan<char> text, int start, int count)
    {
        for (int i = start; i <= text.Length - count; i++)
        {
            if (text[i] == '`' && CountLeadingSpan(text[i..], '`') == count)
                return i;
        }

        return -1;
    }

    private static int FindUnescaped(ReadOnlySpan<char> text, int start, char target)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++; // skip escaped char
                continue;
            }

            if (text[i] == target)
                return i;
        }

        return -1;
    }

    // --- Utility ---

    internal static string FlattenInlinesToText(IReadOnlyList<MarkdownInline> inlines)
    {
        var sb = new StringBuilder();
        FlattenInlinesCore(inlines, sb);
        return sb.ToString();
    }

    private static void FlattenInlinesCore(IReadOnlyList<MarkdownInline> inlines, StringBuilder sb)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    sb.Append(text.Text);
                    break;
                case EmphasisInline emphasis:
                    FlattenInlinesCore(emphasis.Children, sb);
                    break;
                case CodeInline code:
                    sb.Append(code.Code);
                    break;
                case LinkInline link:
                    sb.Append(link.Text);
                    break;
                case ImageInline image:
                    sb.Append(image.AltText);
                    break;
                case LineBreakInline:
                    sb.Append(' ');
                    break;
            }
        }
    }

    /// <summary>
    /// Detects task list checkbox ([ ] or [x]/[X]) at the start of the first paragraph's
    /// inline content. If found, strips the checkbox prefix from the text and returns
    /// the checked state. Returns null for normal list items.
    /// </summary>
    private static bool? ExtractTaskListCheckbox(List<MarkdownBlock> children)
    {
        if (children.Count == 0) return null;
        if (children[0] is not ParagraphBlock para || para.Inlines.Count == 0) return null;
        if (para.Inlines[0] is not TextInline firstText) return null;

        var text = firstText.Text;
        bool? checkedState = null;

        if (text.StartsWith("[ ] ", StringComparison.Ordinal))
            checkedState = false;
        else if (text.Length >= 4 && text[0] == '[' && (text[1] == 'x' || text[1] == 'X') && text[2] == ']' && text[3] == ' ')
            checkedState = true;

        if (checkedState == null) return null;

        var newInlines = new List<MarkdownInline>(para.Inlines);
        var remainder = text[4..];
        if (remainder.Length > 0)
            newInlines[0] = new TextInline(remainder);
        else
            newInlines.RemoveAt(0);

        // Reconstruct the plain text without the checkbox prefix
        var newText = para.Text.Length >= 4 ? para.Text[4..] : "";
        children[0] = new ParagraphBlock(newInlines, newText);
        return checkedState;
    }

    private static bool IsBlankLine(string line) => line.Trim().Length == 0;

    private static int CountLeading(string s, char c)
    {
        int count = 0;
        while (count < s.Length && s[count] == c)
            count++;
        return count;
    }

    private static int CountLeadingSpan(ReadOnlySpan<char> s, char c)
    {
        int count = 0;
        while (count < s.Length && s[count] == c)
            count++;
        return count;
    }

    private static bool HasIndent(string line, int spaces)
    {
        if (line.Length == 0) return false;

        int counted = 0;
        foreach (var c in line)
        {
            if (c == ' ') counted++;
            else if (c == '\t') counted += 4;
            else break;

            if (counted >= spaces) return true;
        }

        return false;
    }

    private static string RemoveIndent(string line, int spaces)
    {
        int removed = 0;
        int i = 0;
        while (i < line.Length && removed < spaces)
        {
            if (line[i] == ' ')
            {
                removed++;
                i++;
            }
            else if (line[i] == '\t')
            {
                removed += 4;
                i++;
            }
            else
            {
                break;
            }
        }

        return i < line.Length ? line[i..] : "";
    }
}
