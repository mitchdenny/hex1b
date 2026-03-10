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
///   <item>Tracking active placements (including multiple fragments per image) to compute minimal diffs</item>
///   <item>Emitting only the necessary delete/transmit/place commands per frame</item>
/// </list>
/// <para>
/// Supports two input modes:
/// <list type="bullet">
///   <item><see cref="GenerateCommands(List{KgpFragment})"/>: Pre-computed fragments from the
///     <see cref="KgpOcclusionSolver"/> (preferred when occlusion is active)</item>
///   <item><see cref="GenerateCommands(Surface)"/>: Backward-compatible surface scanning
///     for simple scenarios without occlusion</item>
/// </list>
/// </para>
/// </remarks>
internal class KgpPlacementTracker
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
    /// Active fragments from the previous frame, grouped by image ID.
    /// Used to determine which images need delete + re-emit.
    /// </summary>
    private readonly Dictionary<uint, List<KgpFragment>> _previousFragments = new();

    /// <summary>
    /// Whether any KGP images have ever been transmitted during this session.
    /// Survives <see cref="Reset"/> so that resize cleanup can send delete-all
    /// even after the per-frame tracking state is cleared.
    /// </summary>
    private bool _hasEverTransmitted;

    /// <summary>
    /// Generates KGP commands from pre-computed occlusion fragments.
    /// </summary>
    /// <param name="fragments">Visible fragments computed by <see cref="KgpOcclusionSolver"/>.</param>
    /// <returns>
    /// Token lists split by z-order: <c>BeforeText</c> (z &lt; 0) and <c>AfterText</c> (z &gt; 0).
    /// </returns>
    public (List<AnsiToken> BeforeText, List<AnsiToken> AfterText) GenerateCommands(List<KgpFragment> fragments)
    {
        var beforeText = new List<AnsiToken>();
        var afterText = new List<AnsiToken>();

        // Group current fragments by image ID
        var currentByImage = new Dictionary<uint, List<KgpFragment>>();
        foreach (var fragment in fragments)
        {
            if (!currentByImage.TryGetValue(fragment.ImageId, out var list))
            {
                list = new List<KgpFragment>();
                currentByImage[fragment.ImageId] = list;
            }
            list.Add(fragment);
        }

        // 1. Delete placements for images that changed or disappeared
        foreach (var (imageId, _) in _previousFragments)
        {
            if (!currentByImage.TryGetValue(imageId, out var currentList) ||
                !FragmentListsEqual(_previousFragments[imageId], currentList))
            {
                beforeText.Add(new UnrecognizedSequenceToken(
                    $"\x1b_Ga=d,d=i,i={imageId},q=2\x1b\\"));
            }
        }

        // 2. Emit placements for new or changed images
        foreach (var (imageId, currentList) in currentByImage)
        {
            var needsTransmit = !_transmittedImages.Contains(imageId);
            var needsPlace = !_previousFragments.TryGetValue(imageId, out var prevList) ||
                             !FragmentListsEqual(prevList, currentList);

            if (!needsTransmit && !needsPlace)
                continue; // Unchanged — skip entirely

            // For the first fragment, handle transmit if needed
            if (needsTransmit && currentList.Count > 0)
            {
                var firstFragment = currentList[0];
                var targetList = firstFragment.Data.ZIndex < 0 ? beforeText : afterText;
                targetList.Add(new CursorPositionToken(firstFragment.AbsoluteY + 1, firstFragment.AbsoluteX + 1));

                var chunks = firstFragment.Data.BuildTransmitChunks();
                foreach (var chunk in chunks)
                {
                    targetList.Add(new UnrecognizedSequenceToken(chunk));
                }
                _transmittedImages.Add(imageId);
                _hasEverTransmitted = true;
            }

            // Emit all fragment placements
            foreach (var fragment in currentList)
            {
                var targetList = fragment.Data.ZIndex < 0 ? beforeText : afterText;
                targetList.Add(new CursorPositionToken(fragment.AbsoluteY + 1, fragment.AbsoluteX + 1));

                // Build placement with fragment-specific clip parameters
                var placementData = fragment.Data.WithClip(
                    fragment.ClipX, fragment.ClipY, fragment.ClipW, fragment.ClipH,
                    fragment.CellWidth, fragment.CellHeight);
                targetList.Add(new UnrecognizedSequenceToken(placementData.BuildPlacementPayload()));
            }
        }

        // 3. Update state for next frame
        _previousFragments.Clear();
        foreach (var (imageId, list) in currentByImage)
        {
            _previousFragments[imageId] = new List<KgpFragment>(list);
        }

        return (beforeText, afterText);
    }

    /// <summary>
    /// Generates KGP commands by scanning the surface for anchor cells.
    /// This is the backward-compatible path for simple scenarios without occlusion.
    /// </summary>
    public (List<AnsiToken> BeforeText, List<AnsiToken> AfterText) GenerateCommands(Surface? currentSurface)
    {
        var placements = ExtractDesiredPlacements(currentSurface);

        // Convert to fragments for the unified path
        var fragments = new List<KgpFragment>();
        foreach (var (_, placement) in placements)
        {
            fragments.Add(new KgpFragment(
                placement.ImageId,
                placement.X,
                placement.Y,
                placement.Data.WidthInCells,
                placement.Data.HeightInCells,
                placement.Data.ClipX,
                placement.Data.ClipY,
                placement.Data.ClipW,
                placement.Data.ClipH,
                placement.Data));
        }

        return GenerateCommands(fragments);
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
        _previousFragments.Clear();
    }

    /// <summary>
    /// Gets whether any KGP images have been transmitted in the current frame state.
    /// </summary>
    internal bool HasTransmittedImages => _transmittedImages.Count > 0;

    /// <summary>
    /// Gets whether any KGP images have ever been transmitted during this session.
    /// Unlike <see cref="HasTransmittedImages"/>, this survives <see cref="Reset"/>.
    /// </summary>
    internal bool HasEverTransmitted => _hasEverTransmitted;

    /// <summary>
    /// Gets the number of images with active placements from the previous frame.
    /// </summary>
    internal int ActivePlacementCount => _previousFragments.Count;

    /// <summary>
    /// Gets the total number of active fragments across all images.
    /// </summary>
    internal int ActiveFragmentCount
    {
        get
        {
            var count = 0;
            foreach (var list in _previousFragments.Values)
                count += list.Count;
            return count;
        }
    }

    /// <summary>
    /// Compares two fragment lists for equality (same count, same positions and clips).
    /// </summary>
    private static bool FragmentListsEqual(List<KgpFragment> a, List<KgpFragment> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].AbsoluteX != b[i].AbsoluteX ||
                a[i].AbsoluteY != b[i].AbsoluteY ||
                a[i].CellWidth != b[i].CellWidth ||
                a[i].CellHeight != b[i].CellHeight ||
                a[i].ClipX != b[i].ClipX ||
                a[i].ClipY != b[i].ClipY ||
                a[i].ClipW != b[i].ClipW ||
                a[i].ClipH != b[i].ClipH)
            {
                return false;
            }
        }

        return true;
    }
}
