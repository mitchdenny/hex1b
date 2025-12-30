using System.Text;

namespace Hex1b.Tokens;

/// <summary>
/// Serializes ANSI tokens back into escape sequence strings.
/// </summary>
public static class AnsiTokenSerializer
{
    /// <summary>
    /// Serializes a list of tokens into an ANSI escape sequence string.
    /// </summary>
    /// <param name="tokens">The tokens to serialize.</param>
    /// <returns>A string containing the serialized ANSI escape sequences.</returns>
    public static string Serialize(IEnumerable<AnsiToken> tokens)
    {
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            sb.Append(Serialize(token));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Serializes a single token into an ANSI escape sequence string.
    /// </summary>
    /// <param name="token">The token to serialize.</param>
    /// <returns>A string containing the serialized ANSI escape sequence.</returns>
    public static string Serialize(AnsiToken token)
    {
        return token switch
        {
            TextToken t => t.Text,
            ControlCharacterToken c => SerializeControlCharacter(c),
            SgrToken sgr => SerializeSgr(sgr),
            CursorPositionToken pos => SerializeCursorPosition(pos),
            CursorShapeToken shape => SerializeCursorShape(shape),
            ClearScreenToken clear => SerializeClearScreen(clear),
            ClearLineToken clear => SerializeClearLine(clear),
            ScrollRegionToken scroll => SerializeScrollRegion(scroll),
            SaveCursorToken save => save.UseDec ? "\x1b" + "7" : "\x1b[s",
            RestoreCursorToken restore => restore.UseDec ? "\x1b" + "8" : "\x1b[u",
            PrivateModeToken pm => SerializePrivateMode(pm),
            OscToken osc => SerializeOsc(osc),
            DcsToken dcs => SerializeDcs(dcs),
            FrameBeginToken => SerializeApc("HEX1BAPP:FRAME:BEGIN"),
            FrameEndToken => SerializeApc("HEX1BAPP:FRAME:END"),
            UnrecognizedSequenceToken unrec => unrec.Sequence,
            _ => throw new ArgumentException($"Unknown token type: {token.GetType().Name}", nameof(token))
        };
    }

    private static string SerializeControlCharacter(ControlCharacterToken token)
    {
        return token.Character switch
        {
            '\n' => "\n",
            '\r' => "\r",
            '\t' => "\t",
            _ => token.Character.ToString()
        };
    }

    private static string SerializeSgr(SgrToken token)
    {
        // ESC [ params m
        return $"\x1b[{token.Parameters}m";
    }

    private static string SerializeCursorPosition(CursorPositionToken token)
    {
        // ESC [ row ; col H
        // Optimize for common cases
        if (token.Row == 1 && token.Column == 1)
            return "\x1b[H";
        if (token.Column == 1)
            return $"\x1b[{token.Row}H";
        return $"\x1b[{token.Row};{token.Column}H";
    }

    private static string SerializeCursorShape(CursorShapeToken token)
    {
        // ESC [ n SP q (DECSCUSR)
        return $"\x1b[{token.Shape} q";
    }

    private static string SerializeClearScreen(ClearScreenToken token)
    {
        // ESC [ n J
        var code = (int)token.Mode;
        return code == 0 ? "\x1b[J" : $"\x1b[{code}J";
    }

    private static string SerializeClearLine(ClearLineToken token)
    {
        // ESC [ n K
        var code = (int)token.Mode;
        return code == 0 ? "\x1b[K" : $"\x1b[{code}K";
    }

    private static string SerializeScrollRegion(ScrollRegionToken token)
    {
        // ESC [ top ; bottom r
        // Reset is represented by Top=1, Bottom=0
        if (token.Top == 1 && token.Bottom == 0)
            return "\x1b[r";
        return $"\x1b[{token.Top};{token.Bottom}r";
    }

    private static string SerializePrivateMode(PrivateModeToken token)
    {
        // ESC [ ? mode h/l
        var suffix = token.Enable ? 'h' : 'l';
        return $"\x1b[?{token.Mode}{suffix}";
    }

    private static string SerializeOsc(OscToken token)
    {
        // ESC ] command ; params ; payload BEL
        // Use BEL as terminator for better compatibility
        // Format: ESC ] command ; params ; payload BEL
        // For hyperlinks (command=8), empty params means: ESC ] 8 ; ; url BEL
        return $"\x1b]{token.Command};{token.Parameters};{token.Payload}\x07";
    }

    private static string SerializeDcs(DcsToken token)
    {
        // ESC P payload ESC \
        return $"\x1bP{token.Payload}\x1b\\";
    }

    private static string SerializeApc(string content)
    {
        // APC (Application Program Command): ESC _ content ESC \
        return $"\x1b_{content}\x1b\\";
    }
}
