namespace Hex1b.Automation;

internal static class TapeBuiltinSyntax
{
    internal static Dictionary<string, Func<TapeParseContext, Func<TapePlayContext, TapeCommandResult>>> Create()
    {
        var syntax = new Dictionary<string, Func<TapeParseContext, Func<TapePlayContext, TapeCommandResult>>>(StringComparer.Ordinal);
        string[] keywords =
        [
            "Set", "Sleep", "Type", "Enter", "Space", "Backspace", "Delete", "Insert",
            "Ctrl", "Alt", "Shift", "Down", "Left", "Right", "Up", "PageUp", "PageDown",
            "ScrollUp", "ScrollDown", "Tab", "Escape", "End", "Hide", "Require", "Show",
            "Output", "Wait", "Source", "Screenshot", "Copy", "Paste", "Env"
        ];
        foreach (var keyword in keywords)
            syntax.Add(keyword, parse => parse.ParseBuiltIn(keyword));
        return syntax;
    }
}
