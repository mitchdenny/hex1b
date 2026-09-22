using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Finds starting positions using a regex pattern that can span multiple lines.
/// Uses the existing FindMultiLinePattern infrastructure.
/// </summary>
internal sealed class FindMultilineRegexStep : IPatternStep
{
    private readonly Regex _regex;
    private readonly FindOptions _options;
    private readonly bool _trimLines;
    private readonly string? _lineSeparator;

    public FindMultilineRegexStep(Regex regex, FindOptions options = default, bool trimLines = false, string? lineSeparator = "\n")
    {
        _regex = regex;
        _options = options;
        _trimLines = trimLines;
        _lineSeparator = lineSeparator;
    }

    public FindOptions Options => _options;

    public StepResult Execute(PatternExecutionState state)
    {
        return StepResult.Succeeded;
    }

    public List<MultilineMatchPosition> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<MultilineMatchPosition>();
        
        // Use existing FindMultiLinePattern infrastructure
        var matches = region.FindMultiLinePattern(_regex, _trimLines, _lineSeparator);
        
        foreach (var match in matches)
        {
            positions.Add(new MultilineMatchPosition(
                match.StartColumn, match.StartLine,
                match.EndColumn, match.EndLine,
                match.Text));
        }
        
        return positions;
    }
}
