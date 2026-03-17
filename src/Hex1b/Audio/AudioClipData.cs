namespace Hex1b;

/// <summary>
/// Stores transmitted audio clip data for the spatial audio protocol.
/// Audio clips are identified by ID and may be played via multiple placements.
/// </summary>
public sealed class AudioClipData
{
    /// <summary>
    /// The audio clip ID assigned by the client.
    /// </summary>
    public uint ClipId { get; }

    /// <summary>
    /// The raw decoded audio data (PCM samples or WAV container).
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// The audio data format.
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    /// Sample rate in Hz.
    /// </summary>
    public uint SampleRate { get; }

    public AudioClipData(uint clipId, byte[] data, AudioFormat format, uint sampleRate)
    {
        ClipId = clipId;
        Data = data;
        Format = format;
        SampleRate = sampleRate;
    }
}
