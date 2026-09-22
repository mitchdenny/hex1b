using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Finds starting positions using a predicate.
/// </summary>
internal sealed class FindPredicateStep : IPatternStep
{
    private readonly Func<CellMatchContext, bool> _predicate;

    public FindPredicateStep(Func<CellMatchContext, bool> predicate)
    {
        _predicate = predicate;
    }

    public StepResult Execute(PatternExecutionState state)
    {
        // This is used during initial position finding
        var context = state.CreateContext();
        return _predicate(context) ? StepResult.Succeeded : StepResult.Failed;
    }

    public List<(int X, int Y)> FindStartingPositions(IHex1bTerminalRegion region)
    {
        var positions = new List<(int X, int Y)>();
        var tempState = new PatternExecutionState(region);
        
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                tempState.X = x;
                tempState.Y = y;
                tempState.MatchStartCell = region.GetCell(x, y);
                tempState.MatchStartPosition = (x, y);
                tempState.PreviousCell = tempState.MatchStartCell;
                tempState.PreviousPosition = (x, y);
                
                var context = tempState.CreateContext();
                if (_predicate(context))
                {
                    positions.Add((x, y));
                }
            }
        }
        
        return positions;
    }
}
