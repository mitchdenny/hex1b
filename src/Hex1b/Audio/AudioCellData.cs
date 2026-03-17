namespace Hex1b;

/// <summary>
/// Cell-level audio producer data, analogous to <see cref="KgpCellData"/> for images.
/// When a cell is tagged with AudioCellData, it acts as a spatial audio source.
/// </summary>
public sealed class AudioCellData
{
    /// <summary>
    /// The audio clip ID to play at this cell position.
    /// </summary>
    public uint ClipId { get; }

    /// <summary>
    /// Volume as a percentage (0-100).
    /// </summary>
    public int Volume { get; }

    /// <summary>
    /// Whether playback should loop.
    /// </summary>
    public bool Loop { get; }

    /// <summary>
    /// The base64-encoded transmit payload for this clip, or null if already transmitted.
    /// Used by AudioPlacementTracker to determine if transmission is needed.
    /// </summary>
    public string? TransmitPayload { get; }

    /// <summary>
    /// Audio data format (needed for transmit command construction).
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    /// Sample rate in Hz (needed for transmit command construction).
    /// </summary>
    public uint SampleRate { get; }

    public AudioCellData(uint clipId, int volume, bool loop,
        string? transmitPayload = null,
        AudioFormat format = AudioFormat.Wav,
        uint sampleRate = 44100)
    {
        ClipId = clipId;
        Volume = Math.Clamp(volume, 0, 100);
        Loop = loop;
        TransmitPayload = transmitPayload;
        Format = format;
        SampleRate = sampleRate;
    }

    /// <summary>
    /// Builds the APC transmit sequence for this audio clip.
    /// Returns chunked sequences if the payload exceeds 4096 bytes.
    /// </summary>
    public List<string> BuildTransmitChunks()
    {
        if (TransmitPayload is null)
            return [];

        const int maxChunk = 4096;
        var chunks = new List<string>();

        // Extract the base64 payload from the stored transmit payload
        // Format: "a=t,i={id},f={fmt},r={rate},q=2;{base64}"
        var semicolonIndex = TransmitPayload.IndexOf(';');
        var controlData = semicolonIndex >= 0 ? TransmitPayload[..semicolonIndex] : TransmitPayload;
        var base64 = semicolonIndex >= 0 ? TransmitPayload[(semicolonIndex + 1)..] : "";

        if (base64.Length <= maxChunk)
        {
            chunks.Add($"\x1b_A{controlData};{base64}\x1b\\");
        }
        else
        {
            var offset = 0;
            var isFirst = true;

            while (offset < base64.Length)
            {
                var remaining = base64.Length - offset;
                var chunkSize = Math.Min(maxChunk, remaining);
                var chunk = base64.Substring(offset, chunkSize);
                var isLast = offset + chunkSize >= base64.Length;

                if (isFirst)
                {
                    chunks.Add($"\x1b_A{controlData},m=1;{chunk}\x1b\\");
                    isFirst = false;
                }
                else if (isLast)
                {
                    chunks.Add($"\x1b_Am=0;{chunk}\x1b\\");
                }
                else
                {
                    chunks.Add($"\x1b_Am=1;{chunk}\x1b\\");
                }

                offset += chunkSize;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Builds the placement command for this audio producer at a given position.
    /// </summary>
    public string BuildPlacementPayload(int column, int row, uint placementId = 0)
    {
        var cmd = $"\x1b_Aa=p,i={ClipId},c={column},R={row},v={Volume}";
        if (Loop) cmd += ",l=1";
        if (placementId > 0) cmd += $",p={placementId}";
        cmd += ",q=2\x1b\\";
        return cmd;
    }
}
