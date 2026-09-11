using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hex1b;

internal sealed class Hwt1ViewState
{
    internal const int MaxSelectionTextLength = 512 * 1024;
    private long _generation;
    private long? _topRowId;
    private long _requestId;
    private long _selectionRequestId;
    private long _lastRequestId;
    private long? _anchorId;
    private long? _cursorId;
    private long? _resolvedStartId;
    private long? _resolvedEndId;
    private long[] _resolvedRowIds = [];
    private (int Start, int End)[] _resolvedColumns = [];
    private string? _selectedText;
    private int _resolvedStartColumn;
    private int _resolvedEndColumn;
    private int _anchorColumn;
    private int _cursorColumn;
    private string _mode = "character";
    private string _status = "none";
    private Hwt1CopyState? _copy;

    internal void GoLive()
    {
        _topRowId = null;
        _anchorId = _cursorId = _resolvedStartId = _resolvedEndId = null;
        _resolvedRowIds = [];
        _resolvedColumns = [];
        _selectedText = null;
        _status = "none";
        _copy = null;
    }

    internal void Handle(JsonElement command, TerminalTextBuffer buffer)
    {
        var requestId = command.GetProperty("requestId").GetInt64();
        if (requestId < 1 || requestId > 9007199254740991)
            throw new InvalidDataException("History requestId must be a positive safe integer.");
        if (requestId <= _lastRequestId)
            return;
        _lastRequestId = requestId;
        Synchronize(buffer);
        if (_status == "valid")
            _ = CaptureSelection(buffer, ResolveTop(buffer));
        switch (command.GetProperty("type").GetString())
        {
            case "viewport":
                _requestId = requestId;
                var top = ResolveTop(buffer);
                if (command.TryGetProperty("live", out var live) && live.GetBoolean())
                    _topRowId = null;
                else
                {
                    var delta = command.GetProperty("delta").GetInt32();
                    top = (int)Math.Clamp((long)top + delta, 0, buffer.HistoryCount);
                    _topRowId = top == buffer.HistoryCount ? null : buffer.RowId(top);
                }
                if (command.TryGetProperty("extend", out var extend))
                {
                    _selectionRequestId = requestId;
                    if (_status == "valid")
                    {
                        var row = extend.GetProperty("row").GetInt32();
                        var column = ReadColumn(extend, buffer);
                        if (row < 0 || row >= buffer.Height)
                            throw new InvalidDataException("Selection extension row is outside the viewport.");
                        _cursorId = buffer.RowId(ResolveTop(buffer) + row);
                        _cursorColumn = column;
                        RememberResolvedExtent(buffer);
                    }
                }
                break;
            case "selection":
                _selectionRequestId = requestId;
                var action = command.GetProperty("action").GetString();
                if (action == "clear")
                {
                    _anchorId = _cursorId = _resolvedStartId = _resolvedEndId = null;
                    _resolvedRowIds = [];
                    _resolvedColumns = [];
                    _selectedText = null;
                    _status = "none";
                    _copy = null;
                    break;
                }
                if (action is not ("start" or "extend"))
                    throw new InvalidDataException("Unsupported selection action.");
                var mode = action == "start" ? command.GetProperty("mode").GetString() : _mode;
                if (mode is not ("character" or "word" or "line" or "rectangle"))
                    throw new InvalidDataException("Unsupported selection mode.");
                var columnValue = ReadColumn(command, buffer);
                if (command.GetProperty("generation").GetString() != Format(buffer.Generation) ||
                    !long.TryParse(command.GetProperty("rowId").GetString(), CultureInfo.InvariantCulture, out var id) ||
                    buffer.FindRow(id) < 0)
                {
                    InvalidateSelection();
                    break;
                }
                if (action == "start")
                {
                    _anchorId = id;
                    _anchorColumn = columnValue;
                    _mode = mode;
                    _status = "valid";
                }
                if (_status != "valid")
                    break;
                _cursorId = id;
                _cursorColumn = columnValue;
                RememberResolvedExtent(buffer);
                break;
            case "copy":
                if ((command.TryGetProperty("selectionRequestId", out var expectedSelection) &&
                     expectedSelection.GetInt64() != _selectionRequestId) ||
                    (command.TryGetProperty("generation", out var expectedGeneration) &&
                     expectedGeneration.GetString() != Format(buffer.Generation)))
                {
                    _copy = new(requestId, "invalidated", null);
                    break;
                }
                var selection = CaptureSelection(buffer, ResolveTop(buffer));
                _copy = new(requestId, selection.Status, selection.Text);
                break;
            default:
                throw new InvalidDataException("Unsupported history command.");
        }
    }

    internal Hwt1History Capture(TerminalTextBuffer buffer)
    {
        Synchronize(buffer);
        var top = ResolveTop(buffer);
        var ids = new string[buffer.Height];
        for (var row = 0; row < ids.Length; row++)
            ids[row] = Format(buffer.RowId(top + row));
        var selection = CaptureSelection(buffer, top);
        if (_copy is { Status: "valid" } && selection.Status != "valid")
            _copy = _copy with { Status = selection.Status, Text = null };
        return new(Format(buffer.Generation), buffer.Alternate ? "alternate" : "main",
            buffer.TotalRows, top, buffer.HistoryCount, _topRowId is null, ids, _requestId, selection, _copy);
    }

    private void Synchronize(TerminalTextBuffer buffer)
    {
        if (_generation != buffer.Generation)
        {
            if (_generation != 0)
            {
                if (_status == "valid")
                    InvalidateSelection();
                _topRowId = null;
            }
            _generation = buffer.Generation;
        }
        if (_status == "valid" &&
            (Missing(_anchorId) || Missing(_cursorId) || Missing(_resolvedStartId) || Missing(_resolvedEndId)))
            InvalidateSelection();
        if (_status == "valid" && _resolvedStartId is long startId)
        {
            var start = buffer.FindRow(startId);
            for (var row = 0; row < _resolvedRowIds.Length; row++)
            {
                if (start + row >= buffer.TotalRows || buffer.RowId(start + row) != _resolvedRowIds[row])
                {
                    InvalidateSelection();
                    break;
                }
            }
        }
        if (_topRowId is long topId && buffer.FindRow(topId) < 0)
            _topRowId = buffer.HistoryCount == 0 ? null : buffer.RowId(0);

        bool Missing(long? id) => id.HasValue && buffer.FindRow(id.Value) < 0;
    }

    private void InvalidateSelection()
    {
        _status = "invalidated";
        _anchorId = _cursorId = _resolvedStartId = _resolvedEndId = null;
        _resolvedRowIds = [];
        _resolvedColumns = [];
        _selectedText = null;
        if (_copy is not null)
            _copy = _copy with { Status = "invalidated", Text = null };
    }

    private int ResolveTop(TerminalTextBuffer buffer)
        => _topRowId is long id ? Math.Clamp(buffer.FindRow(id), 0, buffer.HistoryCount) : buffer.HistoryCount;

    private void RememberResolvedExtent(TerminalTextBuffer buffer)
    {
        if (ResolveExtent(buffer) is not { } extent)
        {
            InvalidateSelection();
            return;
        }
        var (start, end) = extent;
        if (!WithinTextLimit(buffer, start, end))
        {
            InvalidateSelection();
            return;
        }
        _resolvedStartId = buffer.RowId(start.Row);
        _resolvedEndId = buffer.RowId(end.Row);
        _resolvedStartColumn = start.Column;
        _resolvedEndColumn = end.Column;
        _resolvedRowIds = new long[end.Row - start.Row + 1];
        _resolvedColumns = new (int, int)[_resolvedRowIds.Length];
        for (var row = 0; row < _resolvedRowIds.Length; row++)
            _resolvedRowIds[row] = buffer.RowId(start.Row + row);
        _ = CaptureSelection(buffer, 0, updateIntent: true);
    }

    private (BufferPosition Start, BufferPosition End)? ResolveExtent(TerminalTextBuffer buffer)
    {
        var anchor = new BufferPosition(buffer.FindRow(_anchorId!.Value), _anchorColumn);
        var cursor = new BufferPosition(buffer.FindRow(_cursorId!.Value), _cursorColumn);
        if (_mode == "rectangle")
            return (new(Math.Min(anchor.Row, cursor.Row), Math.Min(anchor.Column, cursor.Column)),
                new(Math.Max(anchor.Row, cursor.Row), Math.Max(anchor.Column, cursor.Column)));
        if (Expand(buffer, anchor) is not { } a)
            return null;
        var expandedCursor = cursor >= a.Start && cursor <= a.End ? a : Expand(buffer, cursor);
        if (expandedCursor is not { } c)
            return null;
        return (a.Start < c.Start ? a.Start : c.Start, a.End > c.End ? a.End : c.End);
    }

    private (BufferPosition Start, BufferPosition End)? Expand(TerminalTextBuffer buffer, BufferPosition point)
    {
        var start = new BufferPosition(point.Row, Owner(buffer, point.Row, point.Column));
        var end = new BufferPosition(point.Row, OwnerEnd(buffer, point.Row, point.Column));
        long textLength = 0;
        long visitedColumns = 0;
        if (_mode == "line")
        {
            if (!ConsumeRow(point.Row))
                return null;
            while (start.Row > 0 && buffer.SoftWrap(start.Row - 1))
            {
                if (!ConsumeRow(start.Row - 1))
                    return null;
                start = start with { Row = start.Row - 1 };
            }
            while (end.Row < buffer.TotalRows - 1 && buffer.SoftWrap(end.Row))
            {
                if (!ConsumeRow(end.Row + 1))
                    return null;
                end = end with { Row = end.Row + 1 };
            }
            return (start with { Column = 0 }, end with { Column = buffer.RowWidth(end.Row) - 1 });
        }
        if (_mode != "word")
            return (start, end);
        var firstCell = buffer.Cell(start.Row, start.Column);
        if (!Consume(firstCell, end.Column - start.Column + 1))
            return null;
        var category = Category(firstCell.Character);
        while (Previous(buffer, start) is { } previous)
        {
            var cell = buffer.Cell(previous.Row, previous.Column);
            if (Category(cell.Character) != category)
                break;
            var lastColumn = OwnerEnd(buffer, previous.Row, previous.Column);
            if (!Consume(cell, lastColumn - previous.Column + 1))
                return null;
            start = previous;
        }
        while (Next(buffer, end) is { } next)
        {
            var cell = buffer.Cell(next.Row, next.Column);
            if (Category(cell.Character) != category)
                break;
            var lastColumn = OwnerEnd(buffer, next.Row, next.Column);
            if (!Consume(cell, lastColumn - next.Column + 1))
                return null;
            end = next with { Column = lastColumn };
        }
        return (start, end);

        bool Consume(TerminalCell cell, int columns)
        {
            textLength += cell.Character?.Length ?? 0;
            visitedColumns += Math.Max(1, columns);
            // Wide cells may own two columns per UTF-16 unit. Counting columns also
            // prevents malformed zero-text continuation runs from traversing for free.
            return textLength <= MaxSelectionTextLength && visitedColumns <= 2L * MaxSelectionTextLength;
        }

        bool ConsumeRow(int row)
        {
            for (var column = 0; column < buffer.RowWidth(row); column++)
                if (!Consume(buffer.Cell(row, column), 1))
                    return false;
            return true;
        }
    }

    private Hwt1SelectionState CaptureSelection(TerminalTextBuffer buffer, int top, bool updateIntent = false)
    {
        if (_status != "valid")
            return new(_selectionRequestId, _status, _mode, [], null);
        if (ResolveExtent(buffer) is not { } extent)
        {
            InvalidateSelection();
            return new(_selectionRequestId, _status, _mode, [], null);
        }
        var (start, end) = extent;
        if (!WithinTextLimit(buffer, start, end) ||
            (!updateIntent && (start.Column != _resolvedStartColumn || end.Column != _resolvedEndColumn ||
                buffer.RowId(start.Row) != _resolvedStartId || buffer.RowId(end.Row) != _resolvedEndId)))
        {
            InvalidateSelection();
            return new(_selectionRequestId, _status, _mode, [], null);
        }
        var ranges = new List<Hwt1SelectionRange>();
        var text = new StringBuilder();
        for (var row = start.Row; row <= end.Row; row++)
        {
            var left = _mode == "rectangle" || row == start.Row ? start.Column : 0;
            var right = _mode == "rectangle" || row == end.Row ? end.Column : buffer.RowWidth(row) - 1;
            left = Owner(buffer, row, left);
            right = OwnerEnd(buffer, row, right);
            if (updateIntent)
                _resolvedColumns[row - start.Row] = (left, right);
            else if (_resolvedColumns[row - start.Row] != (left, right))
            {
                InvalidateSelection();
                return new(_selectionRequestId, _status, _mode, [], null);
            }
            if (row >= top && row < top + buffer.Height && left < buffer.Width)
                ranges.Add(new(row - top, left, Math.Min(buffer.Width, right + 1)));
            if (_mode == "rectangle")
            {
                var rowSelection = new TerminalSelection(new(row, left));
                rowSelection.StartSelection();
                rowSelection.MoveCursor(new(row, right));
                text.Append(rowSelection.ExtractText(TextCell, buffer.Width));
                if (row < end.Row)
                    text.Append('\n');
            }
        }
        if (_mode != "rectangle")
        {
            var selection = new TerminalSelection(start);
            selection.StartSelection();
            selection.MoveCursor(end);
            text.Append(selection.ExtractText(TextCell, buffer.Width, buffer.RowWidth));
        }
        var selectedText = text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (updateIntent)
            _selectedText = selectedText;
        else if (!string.Equals(_selectedText, selectedText, StringComparison.Ordinal))
        {
            InvalidateSelection();
            return new(_selectionRequestId, _status, _mode, [], null);
        }
        return new(_selectionRequestId, _status, _mode, ranges, selectedText);

        TerminalCell? TextCell(int row, int column)
        {
            if (column >= buffer.RowWidth(row))
                return null;
            var cell = buffer.Cell(row, column);
            return cell.Character is "\0" or "\uE000" ? cell with { Character = " " } : cell;
        }
    }

    private static int Owner(TerminalTextBuffer buffer, int row, int column)
    {
        while (column > 0 && buffer.Cell(row, column).Character == "" &&
               buffer.Cell(row, column - 1).Sequence == buffer.Cell(row, column).Sequence)
            column--;
        return column;
    }

    private bool WithinTextLimit(TerminalTextBuffer buffer, BufferPosition start, BufferPosition end)
    {
        long length = 0;
        for (var row = start.Row; row <= end.Row; row++)
        {
            var left = _mode == "rectangle" || row == start.Row ? start.Column : 0;
            var right = _mode == "rectangle" || row == end.Row ? end.Column : buffer.RowWidth(row) - 1;
            left = Owner(buffer, row, left);
            right = Math.Min(OwnerEnd(buffer, row, right), buffer.RowWidth(row) - 1);
            for (var column = left; column <= right; column++)
            {
                length += buffer.Cell(row, column).Character?.Length ?? 0;
                if (length > MaxSelectionTextLength)
                    return false;
            }
            if (row < end.Row && (_mode == "rectangle" || !buffer.SoftWrap(row)))
                length++;
            if (length > MaxSelectionTextLength)
                return false;
        }
        return true;
    }

    private static int OwnerEnd(TerminalTextBuffer buffer, int row, int column)
    {
        column = Owner(buffer, row, column);
        while (column + 1 < buffer.RowWidth(row) && buffer.Cell(row, column + 1).Character == "" &&
               buffer.Cell(row, column + 1).Sequence == buffer.Cell(row, column).Sequence)
            column++;
        return column;
    }

    private static BufferPosition? Previous(TerminalTextBuffer buffer, BufferPosition position)
        => position.Column > 0 ? new(position.Row, Owner(buffer, position.Row, position.Column - 1)) :
            position.Row > 0 && buffer.SoftWrap(position.Row - 1)
                ? new(position.Row - 1, Owner(buffer, position.Row - 1, buffer.RowWidth(position.Row - 1) - 1)) : null;

    private static BufferPosition? Next(TerminalTextBuffer buffer, BufferPosition position)
        => position.Column + 1 < buffer.RowWidth(position.Row) ? new(position.Row, position.Column + 1) :
            position.Row + 1 < buffer.TotalRows && buffer.SoftWrap(position.Row)
                ? new(position.Row + 1, 0) : null;

    private static int Category(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        var rune = Rune.GetRuneAt(text, 0);
        if (Rune.IsWhiteSpace(rune))
            return 0;
        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 1 : 2;
    }

    private static int ReadColumn(JsonElement command, TerminalTextBuffer buffer)
    {
        var column = command.GetProperty("column").GetInt32();
        if (column < 0 || column >= buffer.Width)
            throw new InvalidDataException("Selection column is outside the buffer.");
        return column;
    }

    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);
}
