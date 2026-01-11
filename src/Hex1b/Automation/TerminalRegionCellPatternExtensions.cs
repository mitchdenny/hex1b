using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>
/// Extension methods for searching terminal regions with cell patterns.
/// </summary>
public static class TerminalRegionCellPatternExtensions
{
    /// <summary>
    /// Searches for all occurrences of the pattern in this region.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>A result containing all matches found.</returns>
    public static CellPatternSearchResult SearchPattern(
        this IHex1bTerminalRegion region,
        CellPatternSearcher pattern)
    {
        return pattern.Search(region);
    }

    /// <summary>
    /// Searches for the first occurrence of the pattern in this region.
    /// </summary>
    /// <param name="region">The terminal region to search.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>The first match found, or null if no match.</returns>
    public static CellPatternMatch? SearchFirstPattern(
        this IHex1bTerminalRegion region,
        CellPatternSearcher pattern)
    {
        return pattern.SearchFirst(region);
    }

    /// <summary>
    /// Creates a snapshot region from a pattern match.
    /// Uses the match's bounding rectangle.
    /// </summary>
    /// <param name="region">The terminal region.</param>
    /// <param name="match">The pattern match to create a snapshot from.</param>
    /// <returns>A snapshot region covering the match bounds.</returns>
    public static Hex1bTerminalSnapshotRegion CreateSnapshot(
        this IHex1bTerminalRegion region,
        CellPatternMatch match)
    {
        return region.GetRegion(match.Bounds);
    }

    /// <summary>
    /// Creates a snapshot region from a named capture within a match.
    /// </summary>
    /// <param name="region">The terminal region.</param>
    /// <param name="match">The pattern match containing the capture.</param>
    /// <param name="captureName">The name of the capture to create a snapshot from.</param>
    /// <returns>A snapshot region covering the capture bounds.</returns>
    /// <exception cref="ArgumentException">Thrown if the capture name doesn't exist.</exception>
    public static Hex1bTerminalSnapshotRegion CreateSnapshot(
        this IHex1bTerminalRegion region,
        CellPatternMatch match,
        string captureName)
    {
        if (!match.HasCapture(captureName))
        {
            throw new ArgumentException($"Capture '{captureName}' not found in match.", nameof(captureName));
        }

        var bounds = match.GetCaptureBounds(captureName);
        return region.GetRegion(bounds);
    }
}
