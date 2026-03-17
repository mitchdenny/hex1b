namespace Hex1b;

/// <summary>
/// The audio data format for audio clip transmission.
/// Specified by the 'f' key in the control data of an ESC_A sequence.
/// </summary>
public enum AudioFormat
{
    /// <summary>16-bit signed PCM, mono channel (f=1).</summary>
    Pcm16Mono = 1,

    /// <summary>16-bit signed PCM, stereo channels (f=2).</summary>
    Pcm16Stereo = 2,

    /// <summary>WAV file container (f=3). Header parsed to determine format.</summary>
    Wav = 3,
}
