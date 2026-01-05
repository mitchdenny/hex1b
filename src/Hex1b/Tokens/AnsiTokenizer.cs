using System.Globalization;

namespace Hex1b.Tokens;

/// <summary>
/// Parses ANSI text into a list of tokens.
/// </summary>
public static class AnsiTokenizer
{
    /// <summary>
    /// Tokenizes the given text into a list of ANSI tokens.
    /// </summary>
    /// <param name="text">The text to tokenize, which may contain ANSI escape sequences.</param>
    /// <returns>A read-only list of tokens representing the parsed text.</returns>
    public static IReadOnlyList<AnsiToken> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var tokens = new List<AnsiToken>();
        int i = 0;
        int textStart = -1;

        while (i < text.Length)
        {
            // Check for OSC sequence (ESC ] or 0x9D)
            if (TryParseOscSequence(text, i, out var oscConsumed, out var oscCommand, out var oscParams, out var oscPayload, out var oscUseEscBackslash))
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(new OscToken(oscCommand, oscParams, oscPayload, oscUseEscBackslash));
                i += oscConsumed;
            }
            // Check for APC sequence (ESC _ or 0x9F) - used for frame boundaries
            else if (TryParseApcSequence(text, i, out var apcConsumed, out var apcContent))
            {
                FlushTextToken(text, ref textStart, i, tokens);
                var apcToken = apcContent switch
                {
                    "HEX1BAPP:FRAME:BEGIN" => (AnsiToken)FrameBeginToken.Instance,
                    "HEX1BAPP:FRAME:END" => FrameEndToken.Instance,
                    _ => new UnrecognizedSequenceToken($"\x1b_{apcContent}\x1b\\")
                };
                tokens.Add(apcToken);
                i += apcConsumed;
            }
            // Check for DCS sequence (ESC P or 0x90) - Sixel starts with ESC P q
            else if (TryParseDcsSequence(text, i, out var dcsConsumed, out var dcsPayload))
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(new DcsToken(dcsPayload));
                i += dcsConsumed;
            }
            // Check for CSI sequence (ESC [)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                i = ParseCsiSequence(text, i, tokens);
            }
            // Check for SS3 sequence (ESC O) - function keys F1-F4 and arrow keys in application mode
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == 'O' && i + 2 < text.Length)
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(new Ss3Token(text[i + 2]));
                i += 3;
            }
            // Check for DEC save cursor (ESC 7)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '7')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(SaveCursorToken.Dec);
                i += 2;
            }
            // Check for DEC restore cursor (ESC 8)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '8')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(RestoreCursorToken.Dec);
                i += 2;
            }
            // Check for Index (ESC D) - move cursor down, scroll if at bottom
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == 'D')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(IndexToken.Instance);
                i += 2;
            }
            // Check for Reverse Index (ESC M) - move cursor up, scroll if at top
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == 'M')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(ReverseIndexToken.Instance);
                i += 2;
            }
            // Check for G0 character set designation (ESC ( X)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '(' && i + 2 < text.Length)
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(new CharacterSetToken(0, text[i + 2]));
                i += 3;
            }
            // Check for G1 character set designation (ESC ) X)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == ')' && i + 2 < text.Length)
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(new CharacterSetToken(1, text[i + 2]));
                i += 3;
            }
            // Check for Application Keypad Mode (ESC =)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '=')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(KeypadModeToken.ApplicationMode);
                i += 2;
            }
            // Check for Normal Keypad Mode (ESC >)
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '>')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(KeypadModeToken.NormalMode);
                i += 2;
            }
            // Check for control characters
            else if (text[i] == '\n')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(ControlCharacterToken.LineFeed);
                i++;
            }
            else if (text[i] == '\r')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(ControlCharacterToken.CarriageReturn);
                i++;
            }
            else if (text[i] == '\t')
            {
                FlushTextToken(text, ref textStart, i, tokens);
                tokens.Add(ControlCharacterToken.Tab);
                i++;
            }
            else if (text[i] == '\x1b')
            {
                // Unrecognized escape sequence - capture the ESC and following character if any
                FlushTextToken(text, ref textStart, i, tokens);
                if (i + 1 < text.Length)
                {
                    tokens.Add(new UnrecognizedSequenceToken(text.Substring(i, 2)));
                    i += 2;
                }
                else
                {
                    tokens.Add(new UnrecognizedSequenceToken("\x1b"));
                    i++;
                }
            }
            else
            {
                // Regular text - track start position for batching
                if (textStart < 0)
                    textStart = i;

                // Advance by grapheme cluster
                var grapheme = GetGraphemeAt(text, i);
                i += grapheme.Length;
            }
        }

        // Flush any remaining text
        FlushTextToken(text, ref textStart, i, tokens);

        return tokens;
    }

    private static void FlushTextToken(string text, ref int textStart, int currentPos, List<AnsiToken> tokens)
    {
        if (textStart >= 0)
        {
            tokens.Add(new TextToken(text[textStart..currentPos]));
            textStart = -1;
        }
    }

    private static int ParseCsiSequence(string text, int start, List<AnsiToken> tokens)
    {
        // Find the command character (first letter or ~ after ESC [)
        int end = start + 2;
        
        // Check for SGR mouse sequence (ESC [ <) - must be checked before private mode
        if (end < text.Length && text[end] == '<')
        {
            return ParseSgrMouseSequence(text, start, tokens);
        }
        
        // Check for private mode indicator (?)
        bool isPrivateMode = end < text.Length && text[end] == '?';
        if (isPrivateMode)
            end++;

        // Read parameters until we hit a letter or ~ (for special keys like ESC [ 3 ~)
        while (end < text.Length && !char.IsLetter(text[end]) && text[end] != '~')
        {
            end++;
        }

        if (end >= text.Length)
        {
            // Incomplete sequence
            tokens.Add(new UnrecognizedSequenceToken(text[start..]));
            return text.Length;
        }

        var command = text[end];
        var paramStart = start + 2 + (isPrivateMode ? 1 : 0);
        var parameters = text[paramStart..end];

        switch (command)
        {
            case 'm':
                // SGR - Select Graphic Rendition
                tokens.Add(new SgrToken(parameters));
                break;

            case 'H':
            case 'f':
                // Cursor position
                ParseCursorPosition(parameters, tokens);
                break;

            case 'J':
                // Clear screen
                ParseClearScreen(parameters, tokens);
                break;

            case 'K':
                // Clear line
                ParseClearLine(parameters, tokens);
                break;

            case 'h':
            case 'l':
                // Set/reset mode
                if (isPrivateMode && int.TryParse(parameters, out var modeValue))
                {
                    tokens.Add(new PrivateModeToken(modeValue, command == 'h'));
                }
                else
                {
                    // Standard mode or invalid - treat as unrecognized
                    tokens.Add(new UnrecognizedSequenceToken(text[start..(end + 1)]));
                }
                break;

            case 'q':
                // Cursor shape (DECSCUSR)
                if (isPrivateMode || parameters.Contains(' '))
                {
                    // ESC [ n SP q format
                    tokens.Add(new UnrecognizedSequenceToken(text[start..(end + 1)]));
                }
                else
                {
                    ParseCursorShape(parameters, tokens, text, start, end);
                }
                break;

            case 'r':
                // Scroll region (DECSTBM)
                ParseScrollRegion(parameters, tokens);
                break;

            case 's':
                // CSI s with no parameters = ANSI save cursor (SCOSC)
                // CSI left ; right s = DECSLRM (Set Left Right Margins) when DECLRMM is enabled
                // We emit LeftRightMarginToken when parameters are present; the terminal
                // decides whether to interpret it based on DECLRMM state
                if (string.IsNullOrEmpty(parameters))
                {
                    tokens.Add(SaveCursorToken.Ansi);
                }
                else
                {
                    // Parse as potential DECSLRM
                    var marginParts = parameters.Split(';');
                    var left = marginParts.Length > 0 && int.TryParse(marginParts[0], out var l) ? l : 1;
                    var right = marginParts.Length > 1 && int.TryParse(marginParts[1], out var r) ? r : 0;
                    tokens.Add(new LeftRightMarginToken(left, right));
                }
                break;

            case 'u':
                // ANSI restore cursor
                tokens.Add(RestoreCursorToken.Ansi);
                break;

            case 'A':
                // Cursor Up (CUU)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.Up, ParseMoveCount(parameters)));
                break;
                
            case 'B':
                // Cursor Down (CUD)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.Down, ParseMoveCount(parameters)));
                break;
                
            case 'C':
                // Cursor Forward (CUF)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.Forward, ParseMoveCount(parameters)));
                break;
                
            case 'D':
                // Cursor Back (CUB)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.Back, ParseMoveCount(parameters)));
                break;
                
            case 'E':
                // Cursor Next Line (CNL)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.NextLine, ParseMoveCount(parameters)));
                break;
                
            case 'F':
                // Cursor Previous Line (CPL)
                tokens.Add(new CursorMoveToken(CursorMoveDirection.PreviousLine, ParseMoveCount(parameters)));
                break;
                
            case 'G':
                // Cursor Horizontal Absolute (CHA)
                tokens.Add(new CursorColumnToken(ParseMoveCount(parameters)));
                break;
                
            case 'S':
                // Scroll Up (SU) - scroll content up n lines
                tokens.Add(new ScrollUpToken(ParseMoveCount(parameters)));
                break;
                
            case 'T':
                // Scroll Down (SD) - scroll content down n lines
                tokens.Add(new ScrollDownToken(ParseMoveCount(parameters)));
                break;
                
            case 'L':
                // Insert Lines (IL) - insert n blank lines at cursor
                tokens.Add(new InsertLinesToken(ParseMoveCount(parameters)));
                break;
                
            case 'M':
                // Delete Lines (DL) - delete n lines at cursor
                tokens.Add(new DeleteLinesToken(ParseMoveCount(parameters)));
                break;
                
            case 'P':
                // Delete Character (DCH) - delete n characters at cursor
                tokens.Add(new DeleteCharacterToken(ParseMoveCount(parameters)));
                break;
                
            case '@':
                // Insert Character (ICH) - insert n blank characters at cursor
                tokens.Add(new InsertCharacterToken(ParseMoveCount(parameters)));
                break;
                
            case 'X':
                // Erase Character (ECH) - erase n characters at cursor
                tokens.Add(new EraseCharacterToken(ParseMoveCount(parameters)));
                break;
                
            case 'b':
                // Repeat Character (REP) - repeat last graphic character n times
                tokens.Add(new RepeatCharacterToken(ParseMoveCount(parameters)));
                break;
                
            case '~':
                // Special key sequence (ESC [ n ~ or ESC [ n ; m ~)
                ParseSpecialKey(parameters, tokens);
                break;

            default:
                // Unrecognized CSI sequence
                tokens.Add(new UnrecognizedSequenceToken(text[start..(end + 1)]));
                break;
        }

        return end + 1;
    }
    
    private static int ParseSgrMouseSequence(string text, int start, List<AnsiToken> tokens)
    {
        // SGR mouse format: ESC [ < Cb ; Cx ; Cy M (press) or ESC [ < Cb ; Cx ; Cy m (release)
        // start points to ESC, start+2 points to '<'
        int pos = start + 3; // Skip ESC [ <
        
        // Parse button code
        int buttonEnd = pos;
        while (buttonEnd < text.Length && text[buttonEnd] != ';')
            buttonEnd++;
        
        if (buttonEnd >= text.Length || !int.TryParse(text[pos..buttonEnd], out var buttonCode))
        {
            tokens.Add(new UnrecognizedSequenceToken(text[start..(buttonEnd + 1)]));
            return buttonEnd + 1;
        }
        
        // Parse X coordinate
        int xStart = buttonEnd + 1;
        int xEnd = xStart;
        while (xEnd < text.Length && text[xEnd] != ';')
            xEnd++;
        
        if (xEnd >= text.Length || !int.TryParse(text[xStart..xEnd], out var x))
        {
            tokens.Add(new UnrecognizedSequenceToken(text[start..(xEnd + 1)]));
            return xEnd + 1;
        }
        
        // Parse Y coordinate and terminator
        int yStart = xEnd + 1;
        int yEnd = yStart;
        while (yEnd < text.Length && text[yEnd] != 'M' && text[yEnd] != 'm')
            yEnd++;
        
        if (yEnd >= text.Length || !int.TryParse(text[yStart..yEnd], out var y))
        {
            tokens.Add(new UnrecognizedSequenceToken(text[start..(yEnd + 1)]));
            return yEnd + 1;
        }
        
        char terminator = text[yEnd];
        
        // Decode button and modifiers from buttonCode
        var modifiers = Input.Hex1bModifiers.None;
        if ((buttonCode & 4) != 0) modifiers |= Input.Hex1bModifiers.Shift;
        if ((buttonCode & 8) != 0) modifiers |= Input.Hex1bModifiers.Alt;
        if ((buttonCode & 16) != 0) modifiers |= Input.Hex1bModifiers.Control;
        
        var baseButton = buttonCode & ~(4 | 8 | 16 | 32); // Remove modifier and motion bits
        var isMotion = (buttonCode & 32) != 0;
        var isRelease = terminator == 'm';
        
        var button = baseButton switch
        {
            0 => Input.MouseButton.Left,
            1 => Input.MouseButton.Middle,
            2 => Input.MouseButton.Right,
            3 => Input.MouseButton.None, // Release with no button
            64 => Input.MouseButton.ScrollUp,
            65 => Input.MouseButton.ScrollDown,
            _ => Input.MouseButton.None
        };
        
        var action = isRelease ? Input.MouseAction.Up 
            : isMotion ? (button == Input.MouseButton.None ? Input.MouseAction.Move : Input.MouseAction.Drag)
            : Input.MouseAction.Down;
        
        // Convert to 0-based coordinates
        tokens.Add(new SgrMouseToken(button, action, x - 1, y - 1, modifiers, buttonCode));
        
        return yEnd + 1;
    }
    
    private static void ParseSpecialKey(string parameters, List<AnsiToken> tokens)
    {
        // Format: n or n;m where n is key code and m is modifier
        var parts = parameters.Split(';');
        
        if (parts.Length == 0 || !int.TryParse(parts[0], out var keyCode))
        {
            tokens.Add(new SpecialKeyToken(0, 1));
            return;
        }
        
        int modifiers = 1;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var m))
        {
            modifiers = m;
        }
        
        tokens.Add(new SpecialKeyToken(keyCode, modifiers));
    }
    
    private static int ParseMoveCount(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
            return 1;
        return int.TryParse(parameters, out var count) && count > 0 ? count : 1;
    }

    private static void ParseCursorPosition(string parameters, List<AnsiToken> tokens)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            tokens.Add(new CursorPositionToken(1, 1, parameters));
            return;
        }

        var parts = parameters.Split(';');
        int row = 1, col = 1;

        if (parts.Length >= 1 && int.TryParse(parts[0], out var r))
            row = r;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var c))
            col = c;

        tokens.Add(new CursorPositionToken(row, col, parameters));
    }

    private static void ParseClearScreen(string parameters, List<AnsiToken> tokens)
    {
        var mode = string.IsNullOrEmpty(parameters) ? 0 :
                   int.TryParse(parameters, out var m) ? m : 0;

        var clearMode = mode switch
        {
            0 => ClearMode.ToEnd,
            1 => ClearMode.ToStart,
            2 => ClearMode.All,
            3 => ClearMode.AllAndScrollback,
            _ => ClearMode.ToEnd
        };

        tokens.Add(new ClearScreenToken(clearMode));
    }

    private static void ParseClearLine(string parameters, List<AnsiToken> tokens)
    {
        var mode = string.IsNullOrEmpty(parameters) ? 0 :
                   int.TryParse(parameters, out var m) ? m : 0;

        var clearMode = mode switch
        {
            0 => ClearMode.ToEnd,
            1 => ClearMode.ToStart,
            2 => ClearMode.All,
            _ => ClearMode.ToEnd
        };

        tokens.Add(new ClearLineToken(clearMode));
    }

    private static void ParseCursorShape(string parameters, List<AnsiToken> tokens, string text, int start, int end)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            tokens.Add(CursorShapeToken.Default);
            return;
        }

        if (!int.TryParse(parameters, out var shape))
        {
            tokens.Add(new UnrecognizedSequenceToken(text[start..(end + 1)]));
            return;
        }

        var token = shape switch
        {
            0 => CursorShapeToken.Default,
            1 => CursorShapeToken.BlinkingBlock,
            2 => CursorShapeToken.SteadyBlock,
            3 => CursorShapeToken.BlinkingUnderline,
            4 => CursorShapeToken.SteadyUnderline,
            5 => CursorShapeToken.BlinkingBar,
            6 => CursorShapeToken.SteadyBar,
            _ => null
        };

        if (token is not null)
            tokens.Add(token);
        else
            tokens.Add(new UnrecognizedSequenceToken(text[start..(end + 1)]));
    }

    private static void ParseScrollRegion(string parameters, List<AnsiToken> tokens)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            // Reset scroll region
            tokens.Add(ScrollRegionToken.Reset);
            return;
        }

        var parts = parameters.Split(';');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out var top) &&
            int.TryParse(parts[1], out var bottom))
        {
            tokens.Add(new ScrollRegionToken(top, bottom));
        }
        else
        {
            // Invalid - reset
            tokens.Add(ScrollRegionToken.Reset);
        }
    }

    private static bool TryParseOscSequence(string text, int start, out int consumed, out string command, out string parameters, out string payload, out bool useEscBackslash)
    {
        consumed = 0;
        command = "";
        parameters = "";
        payload = "";
        useEscBackslash = false;

        // Check for OSC start: ESC ] (0x1b 0x5d) or 0x9D
        bool isOscStart = false;
        int dataStart = start;

        if (start + 1 < text.Length && text[start] == '\x1b' && text[start + 1] == ']')
        {
            isOscStart = true;
            dataStart = start + 2;
        }
        else if (start < text.Length && text[start] == '\x9d')
        {
            isOscStart = true;
            dataStart = start + 1;
        }

        if (!isOscStart)
            return false;

        // Find the ST (String Terminator): ESC \ (0x1b 0x5c), BEL (\x07), or 0x9C
        int dataEnd = -1;
        int stLength = 0;
        for (int j = dataStart; j < text.Length; j++)
        {
            if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
            {
                dataEnd = j;
                stLength = 2; // ESC \
                useEscBackslash = true;
                break;
            }
            else if (text[j] == '\x07')
            {
                dataEnd = j;
                stLength = 1; // BEL
                break;
            }
            else if (text[j] == '\x9c')
            {
                dataEnd = j;
                stLength = 1; // 0x9C
                break;
            }
        }

        if (dataEnd < 0)
            return false; // No terminator found

        consumed = dataEnd + stLength - start;

        // Extract the OSC data (between ESC ] and ST)
        var oscData = text.Substring(dataStart, dataEnd - dataStart);

        // Parse OSC data: command ; params ; payload
        var firstSemicolon = oscData.IndexOf(';');
        if (firstSemicolon < 0)
        {
            // No semicolons - entire thing is command
            command = oscData;
            return true;
        }

        command = oscData[..firstSemicolon];

        var secondSemicolon = oscData.IndexOf(';', firstSemicolon + 1);
        if (secondSemicolon < 0)
        {
            // Only one semicolon - rest is payload
            payload = oscData[(firstSemicolon + 1)..];
            return true;
        }

        parameters = oscData[(firstSemicolon + 1)..secondSemicolon];
        payload = oscData[(secondSemicolon + 1)..];

        return true;
    }

    private static bool TryParseDcsSequence(string text, int start, out int consumed, out string payload)
    {
        consumed = 0;
        payload = "";

        // Check for DCS start: ESC P (0x1b 0x50) or 0x90
        bool isDcsStart = false;
        int dataStart = start;

        if (start + 1 < text.Length && text[start] == '\x1b' && text[start + 1] == 'P')
        {
            isDcsStart = true;
            dataStart = start + 2;
        }
        else if (start < text.Length && text[start] == '\x90')
        {
            isDcsStart = true;
            dataStart = start + 1;
        }

        if (!isDcsStart)
            return false;

        // Find the ST (String Terminator): ESC \ (0x1b 0x5c) or 0x9C
        int dataEnd = -1;
        for (int j = dataStart; j < text.Length; j++)
        {
            if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
            {
                dataEnd = j;
                consumed = j + 2 - start; // Include ESC \
                break;
            }
            else if (text[j] == '\x9c')
            {
                dataEnd = j;
                consumed = j + 1 - start; // Include 0x9C
                break;
            }
        }

        if (dataEnd < 0)
            return false; // No terminator found

        // Extract the full DCS payload (between DCS header and ST)
        payload = text.Substring(dataStart, dataEnd - dataStart);
        return true;
    }

    private static bool TryParseApcSequence(string text, int start, out int consumed, out string content)
    {
        consumed = 0;
        content = "";

        // Check for APC start: ESC _ (0x1b 0x5f) or 0x9F
        bool isApcStart = false;
        int dataStart = start;

        if (start + 1 < text.Length && text[start] == '\x1b' && text[start + 1] == '_')
        {
            isApcStart = true;
            dataStart = start + 2;
        }
        else if (start < text.Length && text[start] == '\x9f')
        {
            isApcStart = true;
            dataStart = start + 1;
        }

        if (!isApcStart)
            return false;

        // Find the ST (String Terminator): ESC \ (0x1b 0x5c) or 0x9C
        int dataEnd = -1;
        for (int j = dataStart; j < text.Length; j++)
        {
            if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
            {
                dataEnd = j;
                consumed = j + 2 - start; // Include ESC \
                break;
            }
            else if (text[j] == '\x9c')
            {
                dataEnd = j;
                consumed = j + 1 - start; // Include 0x9C
                break;
            }
        }

        if (dataEnd < 0)
            return false; // No terminator found

        // Extract the APC content (between APC header and ST)
        content = text.Substring(dataStart, dataEnd - dataStart);
        return true;
    }

    /// <summary>
    /// Gets the grapheme cluster starting at the given position in the text.
    /// </summary>
    private static string GetGraphemeAt(string text, int index)
    {
        if (index >= text.Length)
            return "";

        var enumerator = StringInfo.GetTextElementEnumerator(text, index);
        if (enumerator.MoveNext())
        {
            return (string)enumerator.Current;
        }
        return text[index].ToString();
    }
}
