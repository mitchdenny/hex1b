using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Hex1b.Automation;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private long _textGeneration = 1;
    private long _nextTextRowId;
    private long[] _textScreenRowIds = [];
    private readonly Dictionary<long, long> _textHistoryRowIds = [];

    internal event Action? PresentationInvalidated;

    private void NotifyPresentationInvalidated()
    {
        _presentation.InvalidatePresentation();
        PresentationInvalidated?.Invoke();
    }

    private void EnsureTextRows()
    {
        if (_textScreenRowIds.Length == _height)
            return;
        _textScreenRowIds = new long[_height];
        for (var row = 0; row < _height; row++)
            _textScreenRowIds[row] = checked(++_nextTextRowId);
    }

    private void InvalidateTextCoordinates()
    {
        _textGeneration = checked(_textGeneration + 1);
        _textScreenRowIds = [];
        _textHistoryRowIds.Clear();
    }

    private void InvalidateTextRows(int first, int last)
    {
        EnsureTextRows();
        for (var row = Math.Max(0, first); row <= Math.Min(last, _height - 1); row++)
            _textScreenRowIds[row] = checked(++_nextTextRowId);
    }

    private void AdvanceTextRowsForScroll(int left, int right)
    {
        EnsureTextRows();
        if (_scrollTop != 0 || _scrollBottom != _height - 1 || left != 0 || right != _width - 1)
        {
            InvalidateTextRows(_scrollTop, _scrollBottom);
            return;
        }
        Array.Copy(_textScreenRowIds, 1, _textScreenRowIds, 0, _height - 1);
        _textScreenRowIds[^1] = checked(++_nextTextRowId);
    }

    private TerminalTextBuffer GetTextBuffer()
    {
        EnsureTextRows();
        var historyCount = _inAlternateScreen ? 0 : _scrollbackBuffer?.Count ?? 0;
        return new TerminalTextBuffer(_textGeneration, _inAlternateScreen, _width, _height, historyCount,
            row =>
            {
                if (row >= historyCount)
                    return _textScreenRowIds[row - historyCount];
                var historyId = _scrollbackBuffer!.GetEntryAt(row).RowId;
                if (!_textHistoryRowIds.TryGetValue(historyId, out var id))
                    _textHistoryRowIds[historyId] = id = checked(++_nextTextRowId);
                return id;
            },
            (row, column) =>
            {
                if (row >= historyCount)
                    return _screenBuffer[row - historyCount, column];
                var cells = _scrollbackBuffer!.GetEntryAt(row).Row.Cells;
                return column < cells.Length ? cells[column] : TerminalCell.Empty;
            },
            row => row >= historyCount ? _width : _scrollbackBuffer!.GetEntryAt(row).Row.Cells.Length);
    }

    internal void HandleBrowserHistoryMessage(Hwt1ViewState view, JsonElement command)
    {
        lock (_bufferLock)
            view.Handle(command, GetTextBuffer());
    }

    internal void ResetBrowserView(Hwt1ViewState view)
    {
        lock (_bufferLock)
            view.GoLive();
    }

    internal bool TryCaptureBrowserSnapshot(Hwt1ViewState view,
        [NotNullWhen(true)] out Hex1bTerminalSnapshot? snapshot,
        [NotNullWhen(true)] out Hwt1History? history,
        out Hmp1TerminalState? remoteState,
        [NotNullWhen(false)] out Task? pendingUpdate)
    {
        lock (_bufferLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            pendingUpdate = _synchronizedOutputCompletion?.Task;
            if (pendingUpdate is not null)
            {
                snapshot = null;
                history = null;
                remoteState = null;
                return false;
            }

            // Checking the mode and capturing must be atomic: another begin marker
            // may arrive after a waiter wakes but before it acquires the buffer lock.
            remoteState = _hmp1State;
            history = view.Capture(GetTextBuffer());
            snapshot = history.Following
                ? CreateSnapshot()
                : new Hex1bTerminalSnapshot(this,
                    CaptureSnapshotState(0, ScrollbackWidth.CurrentTerminal, history.Top),
                    ScrollbackWidth.CurrentTerminal, TerminalCell.Empty);
            return true;
        }
    }
}
