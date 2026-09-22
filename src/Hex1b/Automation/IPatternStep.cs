using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Base interface for pattern steps.
/// </summary>
internal interface IPatternStep
{
    /// <summary>
    /// Executes the step and returns whether it succeeded.
    /// </summary>
    StepResult Execute(PatternExecutionState state);
}
