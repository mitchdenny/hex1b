using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private int PrintRightMargin(int row, int column) =>
        _declrmm && column >= _marginLeft && column <= _marginRight
            ? _marginRight : LineWidth(row) - 1;

    private bool AdvancePrintRow(List<CellImpact>? impacts)
    {
        var oldX = _cursorX;
        var oldY = _cursorY;
        if (_cursorY == _scrollBottom)
        {
            if (!ScrollUp(impacts))
            {
                RestoreValidCursorAfterAbortedScroll(oldX, oldY);
                return false;
            }
        }
        else if (_cursorY < _height - 1)
        {
            _cursorY++;
        }
        _cursorX = _declrmm ? _marginLeft : 0;
        return true;
    }

    private bool WriteGrapheme(string grapheme, int graphemeWidth, List<CellImpact>? impacts, TerminalCell? source = null)
    {
        var textWrapRowId = _pendingWrap && _wraparoundMode ? CaptureTextWrapRow(_cursorY) : null;
        var textWrapColumn = LineWidth(_cursorY);
        var hyperlink = source.HasValue ? source.Value.TrackedHyperlink : _currentHyperlink;

        if (_pendingWrap)
        {
            _pendingWrap = false;
            if (_wraparoundMode)
            {
                // SoftWrap remains stored at the physical row edge until row
                // metadata owns paragraph boundaries throughout the buffer.
                var wrapColumn = _declrmm ? _marginRight : _width - 1;
                ref var wrapCell = ref _screenBuffer[_cursorY, wrapColumn];
                wrapCell = wrapCell with { Attributes = wrapCell.Attributes | CellAttributes.SoftWrap };
                if (!AdvancePrintRow(impacts))
                    return false;
            }
        }

        var rightMargin = PrintRightMargin(_cursorY, _cursorX);
        var availableWidth = rightMargin - (_declrmm ? _marginLeft : 0) + 1;
        if (graphemeWidth > 1 && availableWidth < graphemeWidth)
        {
            // Preserve the existing narrow-terminal policy: discard an
            // unrepresentable glyph and defer wrapping until the next print.
            MoveTextAnchorsForWrap(textWrapRowId, textWrapColumn, _cursorY);
            _pendingWrap = true;
            return true;
        }

        if (graphemeWidth > 1 && _cursorX + graphemeWidth - 1 > rightMargin)
        {
            if (!_wraparoundMode)
                return true;
            textWrapRowId = CaptureTextWrapRow(_cursorY);
            textWrapColumn = rightMargin;
            if (!_declrmm)
            {
                var spacer = _screenBuffer[_cursorY, rightMargin];
                hyperlink?.AddRef();
                SetCell(_cursorY, rightMargin, spacer with
                {
                    Character = " ",
                    IsWideWrapPadding = true,
                    TrackedHyperlink = hyperlink,
                    Attributes = spacer.Attributes | CellAttributes.SoftWrap
                }, impacts);
                ref var physicalEdge = ref _screenBuffer[_cursorY, _width - 1];
                physicalEdge = physicalEdge with { Attributes = physicalEdge.Attributes | CellAttributes.SoftWrap };
            }
            if (!AdvancePrintRow(impacts))
                return false;
            rightMargin = PrintRightMargin(_cursorY, _cursorX);
            if (_cursorX + graphemeWidth - 1 > rightMargin)
            {
                MoveTextAnchorsForWrap(textWrapRowId, textWrapColumn, _cursorY);
                _pendingWrap = true;
                return true;
            }
        }

        if (_insertMode)
            InsertCharacters(graphemeWidth, impacts, printing: true);
        MoveTextAnchorsForWrap(textWrapRowId, textWrapColumn, _cursorY);

        if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
        {
            var leadingCell = _screenBuffer[_cursorY, _cursorX - 1];
            if (leadingCell.Character.Length > 0 && leadingCell.Character != " ")
                SetCell(_cursorY, _cursorX - 1, TerminalCell.Empty, impacts);
        }
        var endColumn = _cursorX + graphemeWidth;
        if (endColumn <= rightMargin &&
            _screenBuffer[_cursorY, endColumn].Character == "" &&
            DisplayWidth.GetGraphemeWidth(_screenBuffer[_cursorY, endColumn - 1].Character) > 1)
            SetCell(_cursorY, endColumn, TerminalCell.Empty, impacts);

        var sequence = ++_writeSequence;
        var writtenAt = _timeProvider.GetUtcNow();
        var attributes = source.HasValue ? source.Value.Attributes :
            _cursorProtected ? _currentAttributes | CellAttributes.Protected : _currentAttributes;
        hyperlink?.AddRef();
        var cell = new TerminalCell(
            grapheme,
            source.HasValue ? source.Value.Foreground : _currentForeground,
            source.HasValue ? source.Value.Background : _currentBackground,
            attributes, sequence, writtenAt, hyperlink,
            source.HasValue ? source.Value.UnderlineColor : _currentUnderlineColor,
            source.HasValue ? source.Value.UnderlineStyle : _currentUnderlineStyle);
        SetCell(_cursorY, _cursorX, cell, impacts);

        _lastPrintedCell = cell;
        _hasLastPrintedCell = true;
        _lastPrintedCellX = _cursorX;
        _lastPrintedCellY = _cursorY;
        _lastPrintedCellWidth = graphemeWidth;
        for (var offset = 1; offset < graphemeWidth; offset++)
        {
            hyperlink?.AddRef();
            SetCell(_cursorY, _cursorX + offset, cell with
            {
                Character = "",
                Attributes = source.HasValue ? cell.Attributes : _currentAttributes
            }, impacts);
        }

        _cursorX += graphemeWidth;
        if (_cursorX > rightMargin)
        {
            _cursorX = rightMargin;
            _pendingWrap = true;
        }
        _pendingGraphemeCombine = _graphemeClusterMode && grapheme.Length > 0 &&
            grapheme.EnumerateRunes().Last().Value == 0x200D;
        return true;
    }
}
