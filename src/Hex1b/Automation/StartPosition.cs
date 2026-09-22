using System.Collections.Immutable;

namespace Hex1b.Automation;

/// <summary>
/// Represents starting position info for pattern matching.
/// </summary>
internal readonly record struct StartPosition(
    int X, int Y,
    int EndX, int EndY,
    int MatchLength,
    int FindStepIndex,
    FindOptions Options);
