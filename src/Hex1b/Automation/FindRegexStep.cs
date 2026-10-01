using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Finds starting positions using a regex pattern (single-line).
/// Uses the existing FindPattern infrastructure.
/// </summary>
internal sealed class FindRegexStep : IPatternStep
{
    private readonly Regex _regex;
    private readonly FindOptions _options;

    public FindRegexStep(Regex regex, FindOptions options = default)
    {
        _regex = regex;
        _options = options;
    }

    public FindOptions Options => _options;

    public StepResult Execute(PatternExecutionState state)
    {
        return StepResult.Succeeded;
    }

    public List<(int X, int Y, int Length)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y, int Length)>();
        
        // Use existing FindPattern infrastructure
        var matches = region.FindPattern(_regex);
        
        foreach (var match in matches)
        {
            positions.Add((match.StartColumn, match.Line, match.Length));
        }
        
        return positions;
    }
}
