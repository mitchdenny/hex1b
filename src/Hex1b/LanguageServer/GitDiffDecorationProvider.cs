using Hex1b.Documents;
using Hex1b.Theming;

namespace Hex1b.LanguageServer;

/// <summary>
/// An in-process <see cref="ITextDecorationProvider"/> that provides syntax highlighting
/// for unified diff format (git diff, patch files). No external language server required.
/// </summary>
/// <remarks>
/// Highlights diff syntax elements:
/// <list type="bullet">
///   <item><c>diff --git</c>, <c>index</c> — file header (bold, dim)</item>
///   <item><c>---</c>, <c>+++</c> — old/new file paths</item>
///   <item><c>@@</c> — hunk headers (cyan)</item>
///   <item><c>+</c> lines — additions (green foreground, tinted background)</item>
///   <item><c>-</c> lines — deletions (red foreground, tinted background)</item>
///   <item>Context lines — default styling</item>
/// </list>
/// </remarks>
public sealed class GitDiffDecorationProvider : ITextDecorationProvider
{
    // ── Shared decoration instances (avoid allocations per frame) ──

    private static readonly TextDecoration DiffHeader = new()
    {
        Foreground = Hex1bColor.FromRgb(255, 200, 60),
        Bold = true,
    };

    private static readonly TextDecoration IndexLine = new()
    {
        Foreground = Hex1bColor.FromRgb(140, 140, 140),
    };

    private static readonly TextDecoration OldFilePath = new()
    {
        Foreground = Hex1bColor.FromRgb(220, 100, 100),
        Bold = true,
    };

    private static readonly TextDecoration NewFilePath = new()
    {
        Foreground = Hex1bColor.FromRgb(100, 220, 100),
        Bold = true,
    };

    private static readonly TextDecoration HunkHeader = new()
    {
        Foreground = Hex1bColor.FromRgb(80, 200, 220),
        Italic = true,
    };

    private static readonly TextDecoration Addition = new()
    {
        Foreground = Hex1bColor.FromRgb(80, 220, 80),
        Background = Hex1bColor.FromRgb(20, 40, 20),
    };

    private static readonly TextDecoration Deletion = new()
    {
        Foreground = Hex1bColor.FromRgb(220, 80, 80),
        Background = Hex1bColor.FromRgb(40, 20, 20),
    };

    private static readonly TextDecoration NoNewline = new()
    {
        Foreground = Hex1bColor.FromRgb(140, 140, 140),
        Italic = true,
    };

    // ── Cached state ──

    private IEditorSession? _session;
    private long _lastVersion = -1;
    private IReadOnlyList<TextDecorationSpan> _cachedSpans = [];

    public void Activate(IEditorSession session)
    {
        _session = session;
    }

    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        // Rebuild cache when document changes
        if (document.Version != _lastVersion)
        {
            _lastVersion = document.Version;
            _cachedSpans = BuildAllSpans(document);
        }

        // Filter to viewport
        var result = new List<TextDecorationSpan>();
        foreach (var span in _cachedSpans)
        {
            if (span.Start.Line >= startLine && span.Start.Line <= endLine)
                result.Add(span);
        }
        return result;
    }

    public void Deactivate()
    {
        _session = null;
        _cachedSpans = [];
    }

    private static IReadOnlyList<TextDecorationSpan> BuildAllSpans(IHex1bDocument document)
    {
        var text = document.GetText();
        var spans = new List<TextDecorationSpan>();
        var lineNumber = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;

            var decoration = ClassifyLine(line);
            if (decoration == null) continue;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(lineNumber, 1),
                new DocumentPosition(lineNumber, line.Length + 1),
                decoration));
        }

        return spans;
    }

    private static TextDecoration? ClassifyLine(string line)
    {
        // Order matters — "---" and "+++" must be checked before "-" and "+"
        if (line.StartsWith("diff ", StringComparison.Ordinal))
            return DiffHeader;

        if (line.StartsWith("index ", StringComparison.Ordinal) ||
            line.StartsWith("similarity index", StringComparison.Ordinal) ||
            line.StartsWith("rename from", StringComparison.Ordinal) ||
            line.StartsWith("rename to", StringComparison.Ordinal) ||
            line.StartsWith("old mode", StringComparison.Ordinal) ||
            line.StartsWith("new mode", StringComparison.Ordinal) ||
            line.StartsWith("new file mode", StringComparison.Ordinal) ||
            line.StartsWith("deleted file mode", StringComparison.Ordinal))
            return IndexLine;

        if (line.StartsWith("--- ", StringComparison.Ordinal))
            return OldFilePath;

        if (line.StartsWith("+++ ", StringComparison.Ordinal))
            return NewFilePath;

        if (line.StartsWith("@@ ", StringComparison.Ordinal))
            return HunkHeader;

        if (line.StartsWith('+'))
            return Addition;

        if (line.StartsWith('-'))
            return Deletion;

        if (line.StartsWith("\\ No newline", StringComparison.Ordinal))
            return NoNewline;

        // Context lines — no decoration
        return null;
    }
}
