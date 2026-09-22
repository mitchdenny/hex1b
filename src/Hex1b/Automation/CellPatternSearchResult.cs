using System.Text;
using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>
/// Collection of all pattern matches found in a search.
/// </summary>
public sealed class CellPatternSearchResult
{
    private readonly List<CellPatternMatch> _matches;

    internal CellPatternSearchResult(List<CellPatternMatch> matches)
    {
        _matches = matches;
    }

    /// <summary>
    /// All matches found.
    /// </summary>
    public IReadOnlyList<CellPatternMatch> Matches => _matches;

    /// <summary>
    /// Whether any matches were found.
    /// </summary>
    public bool HasMatches => _matches.Count > 0;

    /// <summary>
    /// Number of matches found.
    /// </summary>
    public int Count => _matches.Count;

    /// <summary>
    /// First match, or null if none.
    /// </summary>
    public CellPatternMatch? First => _matches.Count > 0 ? _matches[0] : null;

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    internal static CellPatternSearchResult Empty => new(new List<CellPatternMatch>());
}
