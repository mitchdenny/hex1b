using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Result of executing a pattern step.
/// </summary>
internal readonly struct StepResult
{
    public bool Success { get; }
    public List<(int X, int Y)>? StartingPositions { get; }

    private StepResult(bool success, List<(int X, int Y)>? startingPositions = null)
    {
        Success = success;
        StartingPositions = startingPositions;
    }

    public static StepResult Succeeded => new(true);
    public static StepResult Failed => new(false);
    public static StepResult WithStartingPositions(List<(int X, int Y)> positions) => new(true, positions);
}
