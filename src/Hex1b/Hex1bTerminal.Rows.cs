using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private void CopyScreenRow(int source, int destination, int left, int right,
        List<CellImpact>? impacts, bool damageSixel)
    {
        if (left == 0 && right == _width - 1)
            _screenBuffer.SetRendition(destination, _screenBuffer.GetRendition(source));
        if (impacts is null && !damageSixel)
        {
            _screenBuffer.CopyRow(source, destination, left, right);
            return;
        }
        for (var column = left; column <= right; column++)
        {
            var cell = _screenBuffer[source, column];
            // The destination acquires ownership before either row is overwritten.
            cell.TrackedHyperlink?.AddRef();
            SetCell(destination, column, cell, impacts, damageSixel);
        }
    }

    private void ClearScreenRow(int row, int left, int right, TerminalCell cell,
        List<CellImpact>? impacts, bool respectProtection = false, bool damageSixel = true)
    {
        if (!respectProtection && left == 0 && right >= LineWidth(row) - 1)
            _screenBuffer.SetRendition(row, LineRendition.SingleWidth);
        for (var column = left; column <= right; column++)
        {
            if (respectProtection && IsProtectedCell(row, column))
                continue;
            SetCell(row, column, cell, impacts, damageSixel);
        }
    }

    private void ShiftScreenRows(int top, int bottom, int left, int right, bool down,
        List<CellImpact>? impacts, bool damageSixel = true)
    {
        if (down)
        {
            for (var row = bottom; row > top; row--)
                CopyScreenRow(row - 1, row, left, right, impacts, damageSixel);
        }
        else
        {
            for (var row = top; row < bottom; row++)
                CopyScreenRow(row + 1, row, left, right, impacts, damageSixel);
        }
        ClearScreenRow(down ? top : bottom, left, right, CreateEraseCell(), impacts,
            damageSixel: damageSixel);
    }
}
