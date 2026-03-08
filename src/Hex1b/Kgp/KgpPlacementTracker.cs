using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Kgp;

/// <summary>
/// Manages KGP image transmissions and placements across frames.
/// </summary>
/// <remarks>
/// <para>
/// KGP placements are <b>terminal-level state</b> — they persist until explicitly
/// deleted with <c>a=d</c>. This is fundamentally different from text cells which are
/// position-implicit and overwritten by subsequent writes. The tracker provides
/// proper lifecycle management by:
/// </para>
/// <list type="bullet">
///   <item>Tracking which images have been transmitted (transmit once, place many)</item>
///   <item>Tracking active placements to compute minimal diffs between frames</item>
///   <item>Emitting only the necessary delete/transmit/place commands per frame</item>
/// </list>
/// <para>
/// This is integrated into <see cref="Hex1bApp"/> as a persistent field that
/// survives across render frames, unlike <see cref="TrackedObjectStore"/> which is
/// recreated per frame.
/// </para>
/// </remarks>
public class KgpPlacementTracker
{
    /// <summary>
    /// Represents a desired KGP placement extracted from a surface.
    /// </summary>
    internal readonly record struct DesiredPlacement(
        uint ImageId,
        int X,
        int Y,
        KgpCellData Data);

    /// <summary>
    /// Images that have been transmitted to the terminal (image ID → true).
    /// Since we use <c>d=i</c> (delete placements only, keep data), transmitted
    /// images remain in the terminal's store until evicted by its LRU policy.
    /// </summary>
    private readonly HashSet<uint> _transmittedImages = new();

    /// <summary>
    /// Active placements from the previous frame (image ID → position).
    /// Used to determine which placements need to be deleted or moved.
    /// </summary>
    private readonly Dictionary<uint, (int X, int Y)> _previousPlacements = new();

    /// <summary>
    /// Generates the minimal set of KGP tokens needed to transition from the
    /// previous frame's placements to the current frame's desired placements.
    /// </summary>
    /// <param name="currentSurface">The current surface containing desired KGP placements.</param>
    /// <returns>
    /// A list of tokens in correct emission order: deletes first, then
    /// below-text placements (z &lt; 0), with above-text placements (z &gt; 0) in a separate list.
    /// </returns>
    public (List<AnsiToken> BeforeText, List<AnsiToken> AfterText) GenerateCommands(Surface? currentSurface)
    {
        var beforeText = new List<AnsiToken>();
        var afterText = new List<AnsiToken>();

        // Extract desired placements from the current surface
        var desired = ExtractDesiredPlacements(currentSurface);

        // 1. Delete placements that have moved or disappeared
        foreach (var (imageId, prevPos) in _previousPlacements)
        {
            if (!desired.TryGetValue(imageId, out var placement) ||
                placement.X != prevPos.X || placement.Y != prevPos.Y)
            {
                // Placement moved or removed — delete all placements for this image ID
                // Using d=i keeps the image data in the terminal's store
                beforeText.Add(new UnrecognizedSequenceToken(
                    $"\x1b_Ga=d,d=i,i={imageId},q=2\x1b\\"));
            }
        }

        // 2. Add or update placements
        foreach (var (imageId, placement) in desired)
        {
            var needsTransmit = !_transmittedImages.Contains(imageId);
            var needsPlace = !_previousPlacements.TryGetValue(imageId, out var prevPos) ||
                             prevPos.X != placement.X || prevPos.Y != placement.Y;

            if (!needsTransmit && !needsPlace)
                continue; // Unchanged — skip entirely

            var targetList = placement.Data.ZIndex < 0 ? beforeText : afterText;

            // Cursor position for the placement
            targetList.Add(new CursorPositionToken(placement.Y + 1, placement.X + 1));

            if (needsTransmit)
            {
                // First time seeing this image — transmit data + place
                var chunks = placement.Data.BuildTransmitChunks();
                foreach (var chunk in chunks)
                {
                    targetList.Add(new UnrecognizedSequenceToken(chunk));
                }
                _transmittedImages.Add(imageId);
            }

            // Always emit placement (even for already-transmitted images)
            targetList.Add(new UnrecognizedSequenceToken(placement.Data.BuildPlacementPayload()));
        }

        // 3. Update active placements for next frame
        _previousPlacements.Clear();
        foreach (var (imageId, placement) in desired)
        {
            _previousPlacements[imageId] = (placement.X, placement.Y);
        }

        return (beforeText, afterText);
    }

    /// <summary>
    /// Extracts all KGP placements from the surface by scanning for anchor cells.
    /// </summary>
    internal static Dictionary<uint, DesiredPlacement> ExtractDesiredPlacements(Surface? surface)
    {
        var placements = new Dictionary<uint, DesiredPlacement>();
        if (surface == null || !surface.HasKgp)
            return placements;

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];
                if (cell.HasKgp && cell.Kgp?.Data is not null)
                {
                    var data = cell.Kgp.Data;
                    placements.TryAdd(data.ImageId, new DesiredPlacement(data.ImageId, x, y, data));
                }
            }
        }

        return placements;
    }

    /// <summary>
    /// Resets all tracking state. Call when the terminal is resized or cleared.
    /// </summary>
    public void Reset()
    {
        _transmittedImages.Clear();
        _previousPlacements.Clear();
    }

    /// <summary>
    /// Gets whether any KGP images have been transmitted.
    /// </summary>
    internal bool HasTransmittedImages => _transmittedImages.Count > 0;

    /// <summary>
    /// Gets the number of active placements from the previous frame.
    /// </summary>
    internal int ActivePlacementCount => _previousPlacements.Count;
}
