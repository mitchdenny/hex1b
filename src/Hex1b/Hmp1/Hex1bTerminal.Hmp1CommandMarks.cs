using System.Globalization;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    internal Hmp1CommandMarkState? Hmp1ReplayCommandMarkState { get; private set; }

    private Hmp1CommandMarkState CaptureHmp1CommandMarks(int historyRows)
    {
        CollectExpiredTextAnchors();
        var buffer = GetTextBuffer();
        var marks = new List<Hmp1CommandMark>();
        var savedHistory = _inAlternateScreen && historyRows > 0
            ? _scrollbackBuffer!.GetEntries(historyRows) : [];
        var savedRows = new Dictionary<long, int>();
        for (var i = 0; i < savedHistory.Length; i++)
            if (_savedMainTextHistoryRowIds?.TryGetValue(savedHistory[i].RowId, out var id) == true)
                savedRows[id] = i;
        for (var index = _commandMarks.Count - 1; index >= 0 && marks.Count < Hmp1CommandMarkState.MaxMarks; index--)
        {
            var mark = _commandMarks[index];
            var anchor = _commandAnchors[mark];
            int row;
            var column = anchor.Column;
            if (anchor.Alternate == buffer.Alternate)
            {
                if (ResolveTextAnchor(anchor, buffer) is not int resolved)
                    continue;
                row = resolved - (buffer.Alternate ? 0 : buffer.HistoryCount - historyRows);
                column = anchor.Column;
                if (row < 0)
                    continue;
            }
            else
            {
                if (anchor.Alternate || anchor.RowId is not long id || !savedRows.TryGetValue(id, out row))
                    continue;
                var cells = savedHistory[row].Row.Cells;
                if (cells[^1].IsSoftWrap && (column == cells.Length ||
                    (column < cells.Length && cells[column].IsWideWrapPadding)))
                {
                    row++;
                    column = 0;
                    if (row == historyRows)
                        continue; // Saved main-screen cells are not part of StateSync.
                }
            }
            var commandId = long.Parse(anchor.Id.AsSpan("command:".Length), CultureInfo.InvariantCulture);
            marks.Add(new(commandId, anchor.Alternate, row, column, mark.Phase, mark.ExitCode, mark.RawParameters));
        }
        marks.Reverse();
        return new(true, historyRows, _width, _height, _inAlternateScreen, _nextCommandAnchorId,
            _commandMarks.Count, marks.ToArray());
    }

    private void RestoreHmp1CommandMarks(Hmp1CommandMarkState? state)
    {
        if (state is not { Available: true })
            return;
        if (state.Alternate != _inAlternateScreen)
            throw new InvalidDataException("Command checkpoint buffer does not match StateSync.");
        foreach (var anchor in _commandAnchors.Values)
        {
            _textAnchors.Remove(anchor);
            _historyTextAnchors.Remove(anchor);
        }
        _commandMarks.Clear();
        _commandAnchors.Clear();
        ReleaseExpiredMarkerDetails();
        _nextCommandAnchorId = state.LastId;
        if (_commandMarkHistoryCapacity == 0)
            return;
        EnsureTextRows();
        var discarded = Math.Max(0, state.HistoryRows - (_scrollbackBuffer?.Count ?? 0));
        foreach (var mark in state.Marks)
        {
            long rowId;
            var historical = !mark.Alternate && mark.Row < state.HistoryRows;
            if (historical)
            {
                if (mark.Row < discarded || _scrollbackBuffer is null)
                    continue;
                var entry = _scrollbackBuffer.GetEntryAt(mark.Row - discarded);
                var rowIds = _inAlternateScreen
                    ? _savedMainTextHistoryRowIds ??= [] : _textHistoryRowIds;
                if (!rowIds.TryGetValue(entry.RowId, out rowId))
                    rowIds[entry.RowId] = rowId = checked(++_nextTextRowId);
            }
            else
            {
                rowId = _textScreenRowIds[mark.Row - (mark.Alternate ? 0 : state.HistoryRows)];
            }
            var restored = new TerminalCommandMark(mark.Phase, mark.ExitCode, mark.RawParameters, _textGeneration, rowId);
            var anchor = new TerminalTextAnchor($"command:{mark.Id}", mark.Alternate, rowId, mark.Column);
            _commandMarks.Add(restored);
            _commandAnchors.Add(restored, anchor);
            _textAnchors.Add(anchor);
            if (historical)
                _historyTextAnchors.Add(anchor);
        }
        TrimCommandMarks();
    }
}
