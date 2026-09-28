using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private bool UpdateLastGrapheme(string text, int width, List<CellImpact>? impacts,
        bool preserveOnNoWrap = false)
    {
        var x = _lastPrintedCellX;
        var y = _lastPrintedCellY;
        if (y < 0 || y >= _height || x < 0 || x >= LineWidth(y))
            return true;
        var source = _screenBuffer[y, x];
        if (source.Sequence != _lastPrintedCell.Sequence)
            return true;
        var oldWidth = _lastPrintedCellWidth;
        var right = PrintRightMargin(y, x);

        if (width > oldWidth && x + width - 1 > right)
        {
            if (!_wraparoundMode && preserveOnNoWrap)
                return true;
            // Keep the pen alive while the old cells are erased and a scroll
            // potentially releases their history owners.
            source.TrackedHyperlink?.AddRef();
            try
            {
                for (var offset = 0; offset < oldWidth && x + offset <= right; offset++)
                    SetCell(y, x + offset, TerminalCell.Empty, impacts);
                _hasLastPrintedCell = false;
                _pendingGraphemeCombine = false;
                if (!_wraparoundMode)
                    return true;
                _cursorX = x;
                _cursorY = y;
                _pendingWrap = false;
                return WriteGrapheme(text, width, impacts, source);
            }
            finally
            {
                source.TrackedHyperlink?.Release();
            }
        }

        var updated = source with { Character = text };
        source.TrackedHyperlink?.AddRef();
        SetCell(y, x, updated, impacts);
        if (width != oldWidth)
        {
            for (var offset = width; offset < oldWidth && x + offset <= right; offset++)
                SetCell(y, x + offset, TerminalCell.Empty, impacts);
            // Widening can overwrite the head of a neighboring wide glyph.
            var end = x + width;
            if (end <= right && _screenBuffer[y, end].Character == "" &&
                DisplayWidth.GetGraphemeWidth(_screenBuffer[y, end - 1].Character) > 1)
                SetCell(y, end, TerminalCell.Empty, impacts);
            for (var offset = oldWidth; offset < width; offset++)
            {
                source.TrackedHyperlink?.AddRef();
                SetCell(y, x + offset, updated with { Character = "" }, impacts);
            }
            _cursorY = y;
            _cursorX = Math.Min(x + width, right);
            _pendingWrap = x + width > right;
        }
        _lastPrintedCell = updated;
        _lastPrintedCellWidth = width;
        return true;
    }
}
