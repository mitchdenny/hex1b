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
            CursorMoveToken move => SerializeCursorMove(move),
            CursorColumnToken col => SerializeCursorColumn(col),
            CursorRowToken row => SerializeCursorRow(row),
            ClearScreenToken clear => SerializeClearScreen(clear),
            ClearLineToken clear => SerializeClearLine(clear),
            ScrollRegionToken scroll => SerializeScrollRegion(scroll),
            ScrollUpToken su => su.Count == 1 ? "\x1b[S" : $"\x1b[{su.Count}S",
            ScrollDownToken sd => sd.Count == 1 ? "\x1b[T" : $"\x1b[{sd.Count}T",
            InsertLinesToken il => il.Count == 1 ? "\x1b[L" : $"\x1b[{il.Count}L",
            DeleteLinesToken dl => dl.Count == 1 ? "\x1b[M" : $"\x1b[{dl.Count}M",
            InsertCharacterToken ich => ich.Count == 1 ? "\x1b[@" : $"\x1b[{ich.Count}@",
            DeleteCharacterToken dch => dch.Count == 1 ? "\x1b[P" : $"\x1b[{dch.Count}P",
            EraseCharacterToken ech => ech.Count == 1 ? "\x1b[X" : $"\x1b[{ech.Count}X",
            RepeatCharacterToken rep => rep.Count == 1 ? "\x1b[b" : $"\x1b[{rep.Count}b",
            IndexToken => "\x1bD",
            ReverseIndexToken => "\x1bM",
            CharacterSetToken cs => $"\x1b{(cs.Target == 0 ? '(' : ')')}{cs.Charset}",
            KeypadModeToken kp => kp.Application ? "\x1b=" : "\x1b>",
            LeftRightMarginToken lrm => SerializeLeftRightMargin(lrm),
            SaveCursorToken save => save.UseDec ? "\x1b" + "7" : "\x1b[s",
            RestoreCursorToken restore => restore.UseDec ? "\x1b" + "8" : "\x1b[u",
            PrivateModeToken pm => SerializePrivateMode(pm),
            OscToken osc => SerializeOsc(osc),
            DcsToken dcs => SerializeDcs(dcs),
            FrameBeginToken => SerializeApc("HEX1BAPP:FRAME:BEGIN"),
            FrameEndToken => SerializeApc("HEX1BAPP:FRAME:END"),
            Ss3Token ss3 => $"\x1bO{ss3.Character}",
            SgrMouseToken mouse => SerializeSgrMouse(mouse),
            SpecialKeyToken special => SerializeSpecialKey(special),
            UnrecognizedSequenceToken unrec => unrec.Sequence,
            _ => throw new ArgumentException($"Unknown token type: {token.GetType().Name}", nameof(token))
        };
    }

    private static string SerializeLeftRightMargin(LeftRightMarginToken token)
    {
        // ESC [ left ; right s (DECSLRM - Set Left Right Margins)
        // Note: We always include both parameters to distinguish from SCOSC (save cursor)
        // which is ESC [ s with no parameters
        return $"\x1b[{token.Left};{token.Right}s";
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
        // If OriginalParams is set, use it to preserve exact original syntax
        if (token.OriginalParams is not null)
        {
            if (string.IsNullOrEmpty(token.OriginalParams))
                return "\x1b[H";
            return $"\x1b[{token.OriginalParams}H";
        }
        // Fall back to computed form
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

    private static string SerializeCursorMove(CursorMoveToken token)
    {
        // ESC [ n X where X is A/B/C/D/E/F
        var command = token.Direction switch
        {
            CursorMoveDirection.Up => 'A',
            CursorMoveDirection.Down => 'B',
            CursorMoveDirection.Forward => 'C',
            CursorMoveDirection.Back => 'D',
            CursorMoveDirection.NextLine => 'E',
            CursorMoveDirection.PreviousLine => 'F',
            _ => 'A'
        };
        
        // Optimize: omit count if 1
        return token.Count == 1 ? $"\x1b[{command}" : $"\x1b[{token.Count}{command}";
    }

    private static string SerializeCursorColumn(CursorColumnToken token)
    {
        // ESC [ n G (CHA)
        return token.Column == 1 ? "\x1b[G" : $"\x1b[{token.Column}G";
    }
    
    private static string SerializeCursorRow(CursorRowToken token)
    {
        // ESC [ n d (VPA)
        return token.Row == 1 ? "\x1b[d" : $"\x1b[{token.Row}d";
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
        // Preserve original terminator style (ESC \ vs BEL)
        var terminator = token.UseEscBackslash ? "\x1b\\" : "\x07";

        // OSC 8 (hyperlinks) uses format: ESC ] 8 ; params ; url ST (params can be empty, but semicolon required)
        if (token.Command == "8")
        {
            return $"\x1b]{token.Command};{token.Parameters};{token.Payload}{terminator}";
        }

        // For other OSC commands with parameters, include them
        if (!string.IsNullOrEmpty(token.Parameters))
        {
            return $"\x1b]{token.Command};{token.Parameters};{token.Payload}{terminator}";
        }

        // Standard form: ESC ] command ; payload ST
        return $"\x1b]{token.Command};{token.Payload}{terminator}";
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

    private static string SerializeSgrMouse(SgrMouseToken token)
    {
        // SGR mouse format: ESC [ < Cb ; Cx ; Cy M (press) or ESC [ < Cb ; Cx ; Cy m (release)
        // Use RawButtonCode to preserve exact encoding, or reconstruct from button/modifiers
        var terminator = token.Action == Input.MouseAction.Up ? 'm' : 'M';
        // 1-based coordinates for protocol
        return $"\x1b[<{token.RawButtonCode};{token.X + 1};{token.Y + 1}{terminator}";
    }

    private static string SerializeSpecialKey(SpecialKeyToken token)
    {
        // Special key format: ESC [ n ~ or ESC [ n ; m ~
        if (token.Modifiers == 1)
            return $"\x1b[{token.KeyCode}~";
        return $"\x1b[{token.KeyCode};{token.Modifiers}~";
    }
}
