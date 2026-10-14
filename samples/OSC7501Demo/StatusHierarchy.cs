using System.Globalization;
using System.Text;
using Hex1b;

namespace OSC7501Demo;

internal sealed class StatusHierarchy(string id)
{
    internal string Id { get; } = id;
    internal TerminalProgramStatusRecord? Record { get; private set; }
    internal SortedDictionary<string, StatusHierarchy> Children { get; } = new(StringComparer.Ordinal);

    internal static StatusHierarchy Create(TerminalProgramStatusSnapshot snapshot)
    {
        var root = new StatusHierarchy("");
        foreach (var record in snapshot.Records)
        {
            var node = root;
            var path = "";
            if (record.Id.Length > 0)
            {
                foreach (var segment in record.Id.Split('/'))
                {
                    path = path.Length == 0 ? segment : $"{path}/{segment}";
                    if (!node.Children.TryGetValue(segment, out var child))
                    {
                        child = new StatusHierarchy(path);
                        node.Children.Add(segment, child);
                    }
                    node = child;
                }
            }
            node.Record = record;
        }
        return root;
    }

    internal static string DisplayText(string? text)
    {
        if (text is null)
            return "(not reported)";
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Format or UnicodeCategory.Control or
                UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                result.Append($"\\u{rune.Value:X4}");
            else
                result.Append(rune);
        }
        return result.ToString();
    }
}
