using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Position info for a multiline match.
/// </summary>
internal readonly record struct MultilineMatchPosition(
    int StartX, int StartY,
    int EndX, int EndY,
    string Text);
