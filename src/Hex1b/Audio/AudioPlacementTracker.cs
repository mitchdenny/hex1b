using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Tracks audio producer placements across frames and generates minimal
/// escape sequence diffs. Modeled after <see cref="Kgp.KgpPlacementTracker"/>.
/// </summary>
public sealed class AudioPlacementTracker
{
    private readonly HashSet<uint> _transmittedClips = new();
    private readonly Dictionary<uint, List<AudioProducer>> _previousProducers = new();

    /// <summary>
    /// Scans the surface for audio-tagged cells and generates the minimal set of
    /// APC escape sequences needed to synchronize the terminal's audio state.
    /// </summary>
    /// <param name="surface">The rendered surface to scan for audio cells.</param>
    /// <returns>List of escape sequence strings to emit.</returns>
    public List<string> GenerateCommands(Surface surface)
    {
        var commands = new List<string>();

        // Collect current audio producers from the surface
        var currentByClip = new Dictionary<uint, List<AudioProducer>>();

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];
                if (!cell.HasAudio) continue;

                var audioData = cell.Audio!.Data;
                if (!currentByClip.TryGetValue(audioData.ClipId, out var list))
                {
                    list = new List<AudioProducer>();
                    currentByClip[audioData.ClipId] = list;
                }

                list.Add(new AudioProducer(audioData.ClipId, x, y, audioData.Volume, audioData.Loop, audioData));
            }
        }

        // Step 1: Delete disappeared clips
        foreach (var (clipId, previousList) in _previousProducers)
        {
            if (!currentByClip.ContainsKey(clipId) && previousList.Count > 0)
            {
                commands.Add($"\x1b_Aa=d,d=i,i={clipId},q=2\x1b\\");
                _transmittedClips.Remove(clipId);
            }
        }

        // Step 2: Process current clips
        foreach (var (clipId, currentList) in currentByClip)
        {
            var needsTransmit = !_transmittedClips.Contains(clipId);

            // Emit transmit if needed
            if (needsTransmit && currentList.Count > 0)
            {
                var first = currentList[0];
                if (first.CellData.TransmitPayload is not null)
                {
                    var chunks = first.CellData.BuildTransmitChunks();
                    commands.AddRange(chunks);
                }
                _transmittedClips.Add(clipId);
            }

            // Emit placements
            _previousProducers.TryGetValue(clipId, out var previousList);

            for (var i = 0; i < currentList.Count; i++)
            {
                var producer = currentList[i];
                var placementId = (uint)(i + 1);

                var needsPlace = needsTransmit ||
                    previousList is null ||
                    i >= previousList.Count ||
                    !ProducersEqual(previousList[i], producer);

                if (!needsPlace) continue;

                commands.Add(producer.CellData.BuildPlacementPayload(
                    producer.Column, producer.Row, placementId));
            }

            // Stop excess placements
            if (previousList is not null && previousList.Count > currentList.Count)
            {
                for (var i = currentList.Count; i < previousList.Count; i++)
                {
                    commands.Add($"\x1b_Aa=s,i={clipId},p={i + 1},q=2\x1b\\");
                }
            }
        }

        // Step 3: Update state
        _previousProducers.Clear();
        foreach (var (clipId, list) in currentByClip)
        {
            _previousProducers[clipId] = new List<AudioProducer>(list);
        }

        return commands;
    }

    /// <summary>
    /// Resets all tracking state. Next frame will retransmit everything.
    /// </summary>
    public void Reset()
    {
        _transmittedClips.Clear();
        _previousProducers.Clear();
    }

    private static bool ProducersEqual(AudioProducer a, AudioProducer b) =>
        a.ClipId == b.ClipId &&
        a.Column == b.Column &&
        a.Row == b.Row &&
        a.Volume == b.Volume &&
        a.Loop == b.Loop;

    private record struct AudioProducer(
        uint ClipId,
        int Column,
        int Row,
        int Volume,
        bool Loop,
        AudioCellData CellData);
}
