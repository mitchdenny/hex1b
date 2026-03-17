namespace Hex1b;

/// <summary>
/// Parsed audio command from control data key=value pairs in an ESC_A sequence.
/// </summary>
/// <remarks>
/// <para>
/// Audio protocol uses APC sequences with the 'A' prefix:
/// <c>ESC _ A &lt;control-data&gt; ; &lt;payload&gt; ESC \</c>
/// </para>
/// <para>
/// Modeled after the Kitty Graphics Protocol (KGP) which uses 'G'.
/// </para>
/// </remarks>
public sealed class AudioCommand
{
    /// <summary>The overall action (a key). Default: Transmit.</summary>
    public AudioAction Action { get; init; } = AudioAction.Transmit;

    /// <summary>Response suppression (q key). 0=normal, 1=suppress OK, 2=suppress all.</summary>
    public int Quiet { get; init; }

    /// <summary>Audio clip ID (i key). 0 means unspecified.</summary>
    public uint ClipId { get; init; }

    /// <summary>Audio data format (f key). Default: Wav.</summary>
    public AudioFormat Format { get; init; } = AudioFormat.Wav;

    /// <summary>Sample rate in Hz (r key). Default: 44100.</summary>
    public uint SampleRate { get; init; } = 44100;

    /// <summary>Whether more chunked data follows (m key). 0=last/only, 1=more.</summary>
    public int MoreData { get; init; }

    /// <summary>Column position for producer placement (c key).</summary>
    public int Column { get; init; }

    /// <summary>Row position for producer placement (R key).</summary>
    public int Row { get; init; }

    /// <summary>Volume as percentage 0-100 (v key). Default: 100.</summary>
    public int Volume { get; init; } = 100;

    /// <summary>Whether to loop playback (l key). 0=once, 1=loop.</summary>
    public int Loop { get; init; }

    /// <summary>Placement ID for stable slot tracking (p key). 0 means unspecified.</summary>
    public uint PlacementId { get; init; }

    /// <summary>Deletion target (d key). Default: All.</summary>
    public AudioDeleteTarget DeleteTarget { get; init; } = AudioDeleteTarget.All;

    /// <summary>
    /// Parses audio control data into an <see cref="AudioCommand"/>.
    /// </summary>
    /// <param name="controlData">Comma-separated key=value pairs (e.g., "a=t,i=42,f=3").</param>
    /// <returns>A parsed <see cref="AudioCommand"/> with defaults for unspecified keys.</returns>
    public static AudioCommand Parse(string controlData)
    {
        if (string.IsNullOrEmpty(controlData))
            return new AudioCommand();

        var action = AudioAction.Transmit;
        int quiet = 0;
        uint clipId = 0;
        var format = AudioFormat.Wav;
        uint sampleRate = 44100;
        int moreData = 0;
        int column = 0, row = 0;
        int volume = 100;
        int loop = 0;
        uint placementId = 0;
        var deleteTarget = AudioDeleteTarget.All;

        foreach (var pair in controlData.Split(','))
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex < 1 || eqIndex >= pair.Length - 1)
                continue;

            var key = pair[..eqIndex];
            var value = pair[(eqIndex + 1)..];

            switch (key)
            {
                case "a":
                    action = ParseAction(value);
                    break;
                case "q":
                    _ = int.TryParse(value, out quiet);
                    break;
                case "i":
                    _ = uint.TryParse(value, out clipId);
                    break;
                case "f":
                    _ = int.TryParse(value, out var formatInt);
                    format = formatInt switch
                    {
                        1 => AudioFormat.Pcm16Mono,
                        2 => AudioFormat.Pcm16Stereo,
                        3 => AudioFormat.Wav,
                        _ => AudioFormat.Wav,
                    };
                    break;
                case "r":
                    _ = uint.TryParse(value, out sampleRate);
                    break;
                case "m":
                    _ = int.TryParse(value, out moreData);
                    break;
                case "c":
                    _ = int.TryParse(value, out column);
                    break;
                case "R":
                    _ = int.TryParse(value, out row);
                    break;
                case "v":
                    _ = int.TryParse(value, out volume);
                    break;
                case "l":
                    _ = int.TryParse(value, out loop);
                    break;
                case "p":
                    _ = uint.TryParse(value, out placementId);
                    break;
                case "d":
                    deleteTarget = ParseDeleteTarget(value);
                    break;
            }
        }

        return new AudioCommand
        {
            Action = action,
            Quiet = quiet,
            ClipId = clipId,
            Format = format,
            SampleRate = sampleRate,
            MoreData = moreData,
            Column = column,
            Row = row,
            Volume = volume,
            Loop = loop,
            PlacementId = placementId,
            DeleteTarget = deleteTarget,
        };
    }

    private static AudioAction ParseAction(string value)
    {
        if (value.Length != 1)
            return AudioAction.Transmit;

        return value[0] switch
        {
            't' => AudioAction.Transmit,
            'p' => AudioAction.Place,
            's' => AudioAction.Stop,
            'd' => AudioAction.Delete,
            _ => AudioAction.Transmit,
        };
    }

    private static AudioDeleteTarget ParseDeleteTarget(string value)
    {
        if (value.Length != 1)
            return AudioDeleteTarget.All;

        return value[0] switch
        {
            'a' => AudioDeleteTarget.All,
            'i' => AudioDeleteTarget.ById,
            _ => AudioDeleteTarget.All,
        };
    }
}
