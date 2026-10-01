using System.Globalization;
using System.Text.Json;
using Hex1b.Reflow;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private readonly HashSet<TerminalTextAnchor> _textAnchors = [];
    private readonly HashSet<TerminalTextAnchor> _historyTextAnchors = [];
    private readonly Dictionary<TerminalCommandMark, TerminalTextAnchor> _commandAnchors =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TerminalTextAnchor, Hwt1ViewState> _customAnchorViews = [];
    private readonly HashSet<Hwt1ViewState> _markerDetailsViews = [];
    private bool _textAnchorRetentionChanged;
    private bool _textAnchorReflowPending;
    private long _nextCommandAnchorId;
    private long[]? _savedMainTextRowIds;
    private Dictionary<long, long>? _savedMainTextHistoryRowIds;
    private int _customMarkerLimit;
    internal int TextAnchorCount => _textAnchors.Count;

    private void InvalidateTextAnchorRetention() => _textAnchorRetentionChanged = true;

    private long? CaptureTextWrapRow(int row)
    {
        if (_textAnchors.Count == 0 || _declrmm)
            return null;
        EnsureTextRows();
        return _textScreenRowIds[row];
    }

    private void MoveTextAnchorsForWrap(long? sourceRowId, int sourceColumn, int targetRow)
    {
        if (sourceRowId is null)
            return;
        foreach (var anchor in _textAnchors)
        {
            if (anchor.Alternate != _inAlternateScreen || anchor.RowId != sourceRowId ||
                anchor.Column < sourceColumn)
                continue;
            // Commit leading affinity when the wrap occurs, not when a browser
            // happens to observe it: the source row may be evicted in this batch.
            anchor.RowId = _textScreenRowIds[targetRow];
            anchor.Column = 0;
            _historyTextAnchors.Remove(anchor);
        }
    }

    private void RetainTextAnchorsInHistory(long rowId)
    {
        foreach (var anchor in _textAnchors)
            if (!anchor.Alternate && anchor.RowId == rowId)
                _historyTextAnchors.Add(anchor);
    }

    private void ShiftTextAnchorsInRow(int row, int start, int rightEdge, int count,
        bool insert, bool printing = false)
    {
        if (_textAnchors.Count == 0)
            return;
        EnsureTextRows();
        var rowId = _textScreenRowIds[row];
        // An implicit insertion fills a blank cursor position with the new glyph.
        // Explicit ICH instead moves that cell, just like any other retained cell.
        var fillsBlank = insert && printing && _screenBuffer[row, start].Character == " ";
        foreach (var anchor in _textAnchors)
        {
            if (anchor.Alternate != _inAlternateScreen || anchor.RowId != rowId ||
                anchor.Column < start || anchor.Column >= rightEdge ||
                (fillsBlank && anchor.Column == start))
                continue;
            if (insert ? anchor.Column >= rightEdge - count : anchor.Column < start + count)
            {
                anchor.RowId = null;
                InvalidateTextAnchorRetention();
            }
            else
                anchor.Column += insert ? count : -count;
        }
    }

    private void CollectExpiredTextAnchors()
    {
        if (!_textAnchorRetentionChanged || _textAnchorReflowPending)
            return;
        _textAnchorRetentionChanged = false;
        if (_textAnchors.Count == 0)
            return;

        // Row identities, not the active view, determine retention. In particular,
        // the saved main screen and its history remain alive in the alternate screen.
        // History membership is maintained during transfer/reflow, so collection
        // visits only the screen rows and retained marker inventory, not scrollback.
        var retainedRows = new HashSet<long>(_textScreenRowIds);
        if (_savedMainTextRowIds is not null)
            retainedRows.UnionWith(_savedMainTextRowIds);

        var expired = new HashSet<TerminalTextAnchor>();
        foreach (var anchor in _textAnchors)
            if (anchor.RowId is not long rowId ||
                (!retainedRows.Contains(rowId) && !_historyTextAnchors.Contains(anchor)))
            {
                anchor.RowId = null;
                expired.Add(anchor);
            }
        if (expired.Count == 0)
            return;

        _commandMarks.RemoveAll(mark =>
        {
            if (!expired.Contains(_commandAnchors[mark]))
                return false;
            _commandAnchors.Remove(mark);
            return true;
        });
        foreach (var anchor in expired)
        {
            if (_customAnchorViews.Remove(anchor, out var view))
                view.CustomMarkers.Remove(anchor.Id);
            _textAnchors.Remove(anchor);
            _historyTextAnchors.Remove(anchor);
        }
        ReleaseExpiredMarkerDetails();
    }

    private void ReleaseExpiredMarkerDetails()
    {
        if (_markerDetailsViews.Count == 0)
            return;
        var retainedIds = _commandAnchors.Values.Select(anchor => anchor.Id).ToHashSet(StringComparer.Ordinal);
        _markerDetailsViews.RemoveWhere(view =>
        {
            if (view.MarkerResult is not { Details: not null } result)
                return true;
            if (result.MarkerId is not null && retainedIds.Contains(result.MarkerId))
                return false;
            view.MarkerResult = new(result.RequestId, false, "unknown-marker", result.MarkerId);
            return true;
        });
    }

    private TerminalTextAnchor RegisterTextAnchor(string id, TerminalTextBuffer buffer, int row, int column)
    {
        // A wide-cell continuation denotes the owning glyph, not a second character.
        while (column > 0 && column < buffer.RowWidth(row) &&
               buffer.Cell(row, column).Character == "" &&
               buffer.Cell(row, column - 1).Sequence == buffer.Cell(row, column).Sequence)
            column--;
        var anchor = new TerminalTextAnchor(id, buffer.Alternate, buffer.RowId(row), column);
        _textAnchors.Add(anchor);
        if (row < buffer.HistoryCount)
            _historyTextAnchors.Add(anchor);
        return anchor;
    }

    private int? ResolveTextAnchor(TerminalTextAnchor anchor, TerminalTextBuffer buffer)
    {
        if (anchor.Alternate != buffer.Alternate || anchor.RowId is not long id)
            return null;
        var row = buffer.FindRow(id);
        if (row < 0)
        {
            anchor.RowId = null;
            InvalidateTextAnchorRetention();
            return null;
        }
        if (row + 1 < buffer.TotalRows && buffer.SoftWrap(row) &&
            (anchor.Column == buffer.RowWidth(row) ||
             (anchor.Column < buffer.RowWidth(row) && buffer.Cell(row, anchor.Column).IsWideWrapPadding)))
        {
            anchor.RowId = buffer.RowId(++row);
            anchor.Column = 0;
            if (row < buffer.HistoryCount)
                _historyTextAnchors.Add(anchor);
            else
                _historyTextAnchors.Remove(anchor);
        }
        return row;
    }

    private List<(TerminalTextAnchor Anchor, TerminalReflowAnchor Position)> PrepareTextAnchorReflow()
    {
        CollectExpiredTextAnchors();
        var buffer = GetTextBuffer();
        var result = new List<(TerminalTextAnchor, TerminalReflowAnchor)>();
        var id = int.MinValue;
        foreach (var anchor in _textAnchors)
            if (ResolveTextAnchor(anchor, buffer) is int row)
                result.Add((anchor, new(id++, row, anchor.Column, IsTextPosition: true)));
        _textAnchorReflowPending = true;
        return result;
    }

    private void ApplyTextAnchorReflow(
        List<(TerminalTextAnchor Anchor, TerminalReflowAnchor Position)> prepared,
        IReadOnlyList<TerminalReflowAnchor> mapped, int discardedRows)
    {
        var positions = mapped.ToDictionary(a => a.Id);
        var buffer = GetTextBuffer();
        foreach (var (anchor, position) in prepared)
        {
            anchor.RowId = null;
            _historyTextAnchors.Remove(anchor);
            if (positions.TryGetValue(position.Id, out var remapped))
            {
                var row = remapped.Row - discardedRows;
                if (row >= 0 && row < buffer.TotalRows &&
                    remapped.Column >= 0 && remapped.Column <= buffer.RowWidth(row))
                {
                    anchor.RowId = buffer.RowId(row);
                    anchor.Column = remapped.Column;
                    if (row < buffer.HistoryCount)
                        _historyTextAnchors.Add(anchor);
                }
            }
        }
        _textAnchorReflowPending = false;
        InvalidateTextAnchorRetention();
        CollectExpiredTextAnchors();
    }

    private void SaveMainTextCoordinates()
    {
        EnsureTextRows();
        _savedMainTextRowIds = _textScreenRowIds;
        _savedMainTextHistoryRowIds = new(_textHistoryRowIds);
    }

    private void InvalidateTextAnchorsInRange(int top, int bottom, int left, int right,
        bool respectProtection = false)
    {
        if (_textAnchors.Count == 0)
            return;
        EnsureTextRows();
        foreach (var anchor in _textAnchors)
        {
            if (anchor.Alternate != _inAlternateScreen || anchor.RowId is not long id ||
                anchor.Column < left || anchor.Column > right)
                continue;
            var row = Array.IndexOf(_textScreenRowIds, id);
            if (row >= top && row <= bottom &&
                (!respectProtection || !IsProtectedCell(row, Math.Min(anchor.Column, _width - 1))))
            {
                anchor.RowId = null;
                InvalidateTextAnchorRetention();
            }
        }
    }

    private void RestoreMainTextCoordinates()
    {
        foreach (var anchor in _textAnchors)
            if (anchor.Alternate)
                anchor.RowId = null;
        _textScreenRowIds = _savedMainTextRowIds ?? [];
        _textHistoryRowIds.Clear();
        if (_savedMainTextHistoryRowIds is not null)
            foreach (var pair in _savedMainTextHistoryRowIds)
                _textHistoryRowIds.Add(pair.Key, pair.Value);
        _savedMainTextRowIds = null;
        _savedMainTextHistoryRowIds = null;
        InvalidateTextAnchorRetention();
    }

    private Hwt1Marker[] CaptureMarkers(Hwt1ViewState view, TerminalTextBuffer buffer)
    {
        CollectExpiredTextAnchors();
        var markers = new List<Hwt1Marker>(_commandAnchors.Count + view.CustomMarkers.Count);
        foreach (var mark in _commandMarks)
        {
            var anchor = _commandAnchors[mark];
            markers.Add(new(anchor.Id, "command", anchor.Alternate ? "alternate" : "main",
                ResolveTextAnchor(anchor, buffer), anchor.Column, PhaseName(mark.Phase), mark.ExitCode));
        }
        foreach (var anchor in view.CustomMarkers.Values)
            markers.Add(new(anchor.Id, "custom", anchor.Alternate ? "alternate" : "main",
                ResolveTextAnchor(anchor, buffer), anchor.Column));
        return markers.ToArray();
    }

    private void HandleMarkerMessage(Hwt1ViewState view, JsonElement command, TerminalTextBuffer buffer)
    {
        CollectExpiredTextAnchors();
        var requestId = command.GetProperty("requestId").GetInt64();
        if (!view.AcceptMarkerRequest(requestId))
            return;
        _markerDetailsViews.Remove(view);
        var id = command.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String
            ? idValue.GetString() : null;
        view.MarkerResult = new(requestId, false, "invalid-marker");
        if (id is null || id.Length > 80)
            return;
        var action = command.GetProperty("action").GetString();
        if (action == "add")
        {
            if (!id.StartsWith("custom:", StringComparison.Ordinal) ||
                !Guid.TryParseExact(id.AsSpan(7), "D", out _))
                return;
            if (view.CustomMarkers.ContainsKey(id))
            {
                view.MarkerResult = new(requestId, false, "duplicate-marker", id);
                return;
            }
            if (view.CustomMarkers.Count >= _customMarkerLimit)
            {
                view.MarkerResult = new(requestId, false, "marker-limit", id);
                return;
            }
            if (!command.TryGetProperty("column", out var columnValue) || !columnValue.TryGetInt32(out var column) ||
                column < 0 || column > buffer.Width ||
                !command.TryGetProperty("generation", out var generation) ||
                generation.GetString() != buffer.Generation.ToString(CultureInfo.InvariantCulture) ||
                !command.TryGetProperty("rowId", out var rowValue) ||
                !long.TryParse(rowValue.GetString(), CultureInfo.InvariantCulture, out var rowId) ||
                buffer.FindRow(rowId) is not (>= 0 and var row))
            {
                view.MarkerResult = new(requestId, false, "stale-position", id);
                return;
            }
            var anchor = RegisterTextAnchor(id, buffer, row, column);
            view.CustomMarkers.Add(id, anchor);
            _customAnchorViews.Add(anchor, view);
            view.MarkerResult = new(requestId, true, MarkerId: id);
            return;
        }
        view.CustomMarkers.TryGetValue(id, out var target);
        TerminalCommandMark? commandMark = null;
        if (target is null)
            foreach (var pair in _commandAnchors)
                if (pair.Value.Id == id)
                {
                    target = pair.Value;
                    commandMark = pair.Key;
                    break;
                }
        if (target is null)
        {
            view.MarkerResult = new(requestId, false, "unknown-marker", id);
            return;
        }
        switch (action)
        {
            case "remove" when commandMark is null:
                view.CustomMarkers.Remove(id);
                _customAnchorViews.Remove(target);
                _textAnchors.Remove(target);
                _historyTextAnchors.Remove(target);
                view.MarkerResult = new(requestId, true, MarkerId: id);
                break;
            case "jump":
                if (ResolveTextAnchor(target, buffer) is int row)
                {
                    view.JumpToRow(buffer, row);
                    view.MarkerResult = new(requestId, true, MarkerId: id);
                }
                else
                    view.MarkerResult = new(requestId, false,
                        target.Alternate != buffer.Alternate ? "inactive-buffer" : "expired-marker", id);
                break;
            case "details" when commandMark is not null:
                if (commandMark.RawParameters?.Length > 8192)
                    view.MarkerResult = new(requestId, false, "details-too-large", id);
                else
                {
                    view.MarkerResult = new(requestId, true, MarkerId: id,
                        Details: new(PhaseName(commandMark.Phase), commandMark.ExitCode, commandMark.RawParameters));
                    _markerDetailsViews.Add(view);
                }
                break;
        }
    }

    internal void ReleaseBrowserMarkers(Hwt1ViewState view)
    {
        lock (_bufferLock)
        {
            foreach (var anchor in view.CustomMarkers.Values)
            {
                _textAnchors.Remove(anchor);
                _historyTextAnchors.Remove(anchor);
                _customAnchorViews.Remove(anchor);
            }
            view.CustomMarkers.Clear();
            view.MarkerResult = null;
            _markerDetailsViews.Remove(view);
        }
    }

    private static string PhaseName(TerminalShellIntegrationPhase phase) => phase switch
    {
        TerminalShellIntegrationPhase.Prompt => "prompt",
        TerminalShellIntegrationPhase.CommandLine => "commandLine",
        TerminalShellIntegrationPhase.Executing => "executing",
        TerminalShellIntegrationPhase.Finished => "finished",
        _ => "unknown"
    };
}
