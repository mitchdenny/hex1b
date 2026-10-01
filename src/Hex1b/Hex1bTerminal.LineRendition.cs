using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private int LineWidth(int row) => _screenBuffer.LogicalWidth(row);

    private void ClampCursorToLine() =>
        _cursorX = Math.Clamp(_cursorX, 0, LineWidth(_cursorY) - 1);

    private void SetLineRendition(LineRendition rendition, List<CellImpact>? impacts)
    {
        if (_declrmm)
            return;
        _pendingWrap = false;
        _screenBuffer.SetRendition(_cursorY, rendition);
        for (var x = 0; x < _width; x++)
        {
            ref var cell = ref _screenBuffer[_cursorY, x];
            cell = cell with { Attributes = cell.Attributes & ~CellAttributes.SoftWrap };
        }
        CropLineRendition(_cursorY, impacts);
        _cursorX = Math.Min(_cursorX, LineWidth(_cursorY) - 1);
    }

    private void CropLineRendition(int row, List<CellImpact>? impacts = null)
    {
        var softWrapped = _screenBuffer[row, _width - 1].IsSoftWrap;
        var width = LineWidth(row);
        var start = width;
        if (width < _width && _screenBuffer[row, width].Character == "")
            while (start > 0 && _screenBuffer[row, start].Character == "")
                start--;
        if (start < _width)
            InvalidateTextAnchorsInRange(row, row, start, _width);
        for (var x = start; x < _width; x++)
            SetCell(row, x, CreateEraseCell(), impacts);
        if (softWrapped)
        {
            ref var edge = ref _screenBuffer[row, _width - 1];
            edge = edge with { Attributes = edge.Attributes | CellAttributes.SoftWrap };
        }
    }
}
