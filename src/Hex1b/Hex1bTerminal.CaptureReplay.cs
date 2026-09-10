using System.Text;
using Hex1b.Sixel;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    /// <summary>
    /// Seeds an ANSI receiver with text buffers and the continuation state needed by
    /// later output. Unlike a display-only export, this retains underlying hidden
    /// characters, protection, saved cursors, margins, tab stops, and active rendition.
    /// </summary>
    private string CreateCaptureReplayUnsafe()
    {
        if (_kgpGraphicsState.HasResidentState || _sixelGraphicsState.HasResidentState)
            throw new NotSupportedException("Live capture cannot seed resident terminal graphics.");
        if (_hasLastPrintedCell &&
            (_lastPrintedCellX < 0 || _lastPrintedCellX >= _width ||
             _lastPrintedCellY < 0 || _lastPrintedCellY >= _height))
            throw new NotSupportedException("Live capture cannot seed a last-printed character outside the viewport.");

        // Even an erased Sixel may have changed registers used by a later graphic.
        var defaultRegisters = new SixelColorRegisters(_sixelColorRegisters.Policy);
        for (var register = 0; register < defaultRegisters.Count; register++)
        {
            if (_sixelColorRegisters.Get(register) != defaultRegisters.Get(register))
                throw new NotSupportedException("Live capture cannot seed modified Sixel color registers.");
        }

        var output = new StringBuilder("\u001bc\x1b[?1049l\x1b[?6l\x1b[?69l\x1b[4l\x1b[20l\x1b[?7h");
        if (_inAlternateScreen && _savedMainScreenBuffer is { } main)
        {
            AppendCaptureScreen(output, main);
            AppendCaptureCursor(output, _alternateScreenSavedCursorX, _alternateScreenSavedCursorY);
            output.Append("\x1b[?1049h");
        }
        AppendCaptureScreen(output, _screenBuffer);

        // Rebuild tab stops while coordinates are still absolute.
        output.Append("\x1b[3g");
        for (var column = 0; column < _tabStops.Length; column++)
        {
            if (_tabStops[column])
            {
                AppendCaptureCursor(output, column, 0);
                output.Append("\x1bH");
            }
        }

        if (_cursorSaved)
        {
            AppendCaptureCursor(output, _savedCursorX, _savedCursorY);
            if (_savedPendingWrap)
                AppendCapturePendingWrap(output, _savedCursorX, _savedCursorY);
            AppendCaptureProtection(output, _savedCursorProtected, _protectedMode);
            output.Append("\u001b7");
        }

        // REP and split grapheme continuation refer to the last printed cell, not
        // simply the cell before the cursor. Repaint that cell last when it survives.
        if (_hasLastPrintedCell && _lastPrintedCellX >= 0 && _lastPrintedCellX < _width &&
            _lastPrintedCellY >= 0 && _lastPrintedCellY < _height)
        {
            var current = _screenBuffer[_lastPrintedCellY, _lastPrintedCellX];
            if (SameCaptureCell(current, _lastPrintedCell))
            {
                AppendCaptureCursor(output, _lastPrintedCellX, _lastPrintedCellY);
                AppendCaptureCell(output, _lastPrintedCell, _protectedMode);
            }
            else if (current.Character == " " && _lastPrintedCellWidth == 1)
            {
                // ECH restores an erased cell without changing REP's last character.
                AppendCaptureCursor(output, _lastPrintedCellX, _lastPrintedCellY);
                AppendCaptureCell(output, _lastPrintedCell, _protectedMode);
                AppendCaptureCursor(output, _lastPrintedCellX, _lastPrintedCellY);
                AppendCaptureRendition(output, current);
                output.Append("\x1b[X");
            }
            else
            {
                throw new NotSupportedException(
                    "Live capture cannot seed a last-printed character whose original cell has been replaced.");
            }
        }

        output.Append($"\x1b[{_scrollTop + 1};{_scrollBottom + 1}r");
        AppendCaptureMode(output, 69, _declrmm);
        if (_declrmm)
            output.Append($"\x1b[{_marginLeft + 1};{_marginRight + 1}s");
        AppendCaptureMode(output, 6, _originMode);
        AppendCaptureCursor(output, _cursorX - (_originMode ? _marginLeft : 0),
            _cursorY - (_originMode ? _scrollTop : 0));
        if (_pendingWrap)
            AppendCapturePendingWrap(output, _cursorX, _cursorY, _originMode);

        AppendCaptureRendition(output, new("", _currentForeground, _currentBackground,
            _currentAttributes, UnderlineColor: _currentUnderlineColor, UnderlineStyle: _currentUnderlineStyle));
        AppendCaptureProtection(output, _cursorProtected, _protectedMode);
        AppendCaptureHyperlink(output, _currentHyperlink?.Data);

        output.Append($"\x1b({_charsetG0}\x1b){_charsetG1}\x1b*{_charsetG2}\x1b+{_charsetG3}");
        output.Append(_activeCharsetSlot switch { 1 => "\x0e", 2 => "\x1bn", 3 => "\x1bo", _ => "\x0f" });
        output.Append(_newlineMode ? "\x1b[20h" : "\x1b[20l");
        output.Append(_insertMode ? "\x1b[4h" : "\x1b[4l");
        AppendCaptureMode(output, 7, _wraparoundMode);
        AppendCaptureMode(output, 45, _reverseWrapMode);
        AppendCaptureMode(output, 1045, _reverseWrapExtendedMode);
        AppendCaptureMode(output, 2027, _graphemeClusterMode);
        AppendCaptureMode(output, 1, _appCursorKeysMode);
        AppendCaptureMode(output, 25, _cursorVisible);
        AppendCaptureMode(output, 2004, _bracketedPasteMode);
        AppendCaptureMode(output, 1004, _focusEventReporting);
        AppendCaptureMode(output, 9, _mouseProtocolX10);
        AppendCaptureMode(output, 1000, _mouseProtocolNormal);
        AppendCaptureMode(output, 1001, _mouseProtocolHighlight);
        AppendCaptureMode(output, 1002, _mouseProtocolButton);
        AppendCaptureMode(output, 1003, _mouseProtocolAny);
        AppendCaptureMode(output, 1005, _mouseEncodingUtf8);
        AppendCaptureMode(output, 1006, _mouseEncodingSgr);
        AppendCaptureMode(output, 1015, _mouseEncodingUrxvt);
        AppendCaptureMode(output, 8452, _sixelCursorToRightMode);
        var sixelMode = _sixelColorRegisters.Policy.ResolveSixelScrolling(true) == _sixelScrollingMode;
        AppendCaptureMode(output, 80, sixelMode);
        output.Append(_appKeypadMode ? "\x1b=" : "\x1b>");
        output.Append($"\x1b[{_cursorShape} q");

        foreach (var (title, icon) in _titleStack.Reverse())
        {
            AppendCaptureTitle(output, title, icon);
            output.Append("\x1b[22;0t");
        }
        AppendCaptureTitle(output, _windowTitle, _iconName);
        return output.ToString();
    }

    private void AppendCaptureScreen(StringBuilder output, TerminalCell[,] cells)
    {
        output.Append("\x1b[0m\x1b[2J\x1b[H");
        var width = Math.Min(_width, cells.GetLength(1));
        var height = Math.Min(_height, cells.GetLength(0));
        for (var row = 0; row < height; row++)
        {
            var continuation = row > 0 && cells.GetLength(1) == _width &&
                cells[row - 1, width - 1].IsSoftWrap;
            if (!continuation)
                AppendCaptureCursor(output, 0, row);
            for (var column = 0; column < width; column++)
            {
                var cell = cells[row, column];
                if (string.IsNullOrEmpty(cell.Character))
                    continue;
                if (!_hasLastPrintedCell)
                {
                    // A never-printed terminal must not gain a REP character merely
                    // because its seed painted blank cells.
                    AppendCaptureCursor(output, column, row);
                    AppendCaptureRendition(output, cell);
                    output.Append("\x1b[X");
                    continue;
                }
                AppendCaptureCell(output, cell, _protectedMode);
            }
        }
    }

    private void AppendCapturePendingWrap(StringBuilder output, int x, int y, bool origin = false)
    {
        var start = x;
        while (start > 0 && string.IsNullOrEmpty(_screenBuffer[y, start].Character))
            start--;
        AppendCaptureCursor(output, start - (origin ? _marginLeft : 0), y - (origin ? _scrollTop : 0));
        AppendCaptureCell(output, _screenBuffer[y, start], _protectedMode);
    }

    private static bool SameCaptureCell(TerminalCell first, TerminalCell second) =>
        first.Character == second.Character && Nullable.Equals(first.Foreground, second.Foreground) &&
        Nullable.Equals(first.Background, second.Background) &&
        (first.Attributes & ~CellAttributes.SoftWrap) == (second.Attributes & ~CellAttributes.SoftWrap) &&
        Nullable.Equals(first.UnderlineColor, second.UnderlineColor) && first.UnderlineStyle == second.UnderlineStyle;

    private static void AppendCaptureCell(StringBuilder output, TerminalCell cell, ProtectedMode protection)
    {
        AppendCaptureRendition(output, cell);
        AppendCaptureProtection(output, (cell.Attributes & CellAttributes.Protected) != 0, protection);
        AppendCaptureHyperlink(output, cell.HyperlinkData);
        output.Append(cell.Character is "\0" or "\uE000" ? " " : cell.Character);
    }

    private static void AppendCaptureRendition(StringBuilder output, TerminalCell cell)
    {
        output.Append("\x1b[0");
        if (cell.IsBold) output.Append(";1");
        if (cell.IsDim) output.Append(";2");
        if (cell.IsItalic) output.Append(";3");
        if (cell.IsUnderline)
            output.Append(cell.UnderlineStyle > UnderlineStyle.Single ? $";4:{(int)cell.UnderlineStyle}" : ";4");
        if (cell.IsBlink) output.Append(";5");
        if (cell.IsReverse) output.Append(";7");
        if (cell.IsHidden) output.Append(";8");
        if (cell.IsStrikethrough) output.Append(";9");
        if (cell.IsOverline) output.Append(";53");
        AppendCaptureColor(output, cell.Foreground, foreground: true);
        AppendCaptureColor(output, cell.Background, foreground: false);
        if (cell.UnderlineColor is { } underline && !underline.IsDefault)
        {
            if (underline.Kind == Hex1bColorKind.Rgb)
                output.Append($";58;2;{underline.R};{underline.G};{underline.B}");
            else
                output.Append($";58;5;{underline.AnsiIndex + (underline.Kind == Hex1bColorKind.Bright ? 8 : 0)}");
        }
        output.Append('m');
    }

    private static void AppendCaptureColor(StringBuilder output, Hex1bColor? color, bool foreground)
    {
        if (color is not { IsDefault: false } value)
            return;
        output.Append(value.Kind switch
        {
            Hex1bColorKind.Standard => $";{(foreground ? 30 : 40) + value.AnsiIndex}",
            Hex1bColorKind.Bright => $";{(foreground ? 90 : 100) + value.AnsiIndex}",
            Hex1bColorKind.Indexed => $";{(foreground ? 38 : 48)};5;{value.AnsiIndex}",
            _ => $";{(foreground ? 38 : 48)};2;{value.R};{value.G};{value.B}"
        });
    }

    private static void AppendCaptureProtection(StringBuilder output, bool enabled, ProtectedMode mode)
    {
        if (mode == ProtectedMode.Iso)
            output.Append(enabled ? "\x1bV" : "\x1bW");
        else if (mode == ProtectedMode.Dec || enabled)
            output.Append(enabled ? "\x1b[1\"q" : "\x1b[0\"q");
    }

    private static void AppendCaptureHyperlink(StringBuilder output, HyperlinkData? hyperlink) =>
        output.Append(AnsiTokenSerializer.Serialize(new OscToken("8",
            hyperlink?.Parameters ?? "", hyperlink?.Uri ?? "", UseEscBackslash: true)));

    private static void AppendCaptureMode(StringBuilder output, int mode, bool enabled) =>
        output.Append($"\x1b[?{mode}{(enabled ? 'h' : 'l')}");

    private static void AppendCaptureCursor(StringBuilder output, int x, int y) =>
        output.Append($"\x1b[{y + 1};{x + 1}H");

    private static void AppendCaptureTitle(StringBuilder output, string title, string icon)
    {
        output.Append(AnsiTokenSerializer.Serialize(new OscToken("2", "", title, UseEscBackslash: true)));
        output.Append(AnsiTokenSerializer.Serialize(new OscToken("1", "", icon, UseEscBackslash: true)));
    }
}
