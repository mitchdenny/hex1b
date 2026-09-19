namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    internal Hmp1ScrollbackState? Hmp1ReplayScrollbackState { get; private set; }

    internal Automation.Hex1bTerminalSnapshot CaptureHmp1Snapshot(int rowLimit, bool includeCommandMarks,
        out Hmp1ScrollbackState? history, out Hmp1CommandMarkState? commands)
    {
        lock (_bufferLock)
        {
            history = rowLimit > 0 ? CaptureHmp1Scrollback(rowLimit) : null;
            commands = includeCommandMarks ? CaptureHmp1CommandMarks(history?.Rows.Count ?? 0) : null;
            return CreateSnapshot(includeAllKgpImages: true, includeSavedTitles: true);
        }
    }

    internal Hmp1ScrollbackState CaptureHmp1Scrollback(int rowLimit)
    {
        lock (_bufferLock)
        {
            if (_scrollbackBuffer is null)
                return Hmp1ScrollbackState.Unavailable;
            var rows = new List<byte[]>();
            var bytes = 0;
            var cells = 0;
            for (var i = _scrollbackBuffer.Count - 1; i >= 0 && rows.Count < rowLimit; i--)
            {
                var row = _scrollbackBuffer.GetEntryAt(i).Row;
                if (cells + row.Cells.Length > Hmp1ScrollbackState.MaxCells)
                    break;
                var encoded = Hmp1ScrollbackRowCodec.Encode(row);
                if (bytes + 4 + encoded.Length > Hmp1ScrollbackState.MaxTotalBytes)
                    break;
                bytes += 4 + encoded.Length;
                cells += row.Cells.Length;
                rows.Add(encoded);
            }
            rows.Reverse();
            return new(_scrollbackBuffer.Count, rows);
        }
    }

    private void RestoreHmp1Scrollback(Hmp1ScrollbackState? state)
    {
        if (state is null || state.AvailableRows < 0 || _scrollbackBuffer is null)
            return;
        _scrollbackBuffer.Clear();
        // History belongs to the main buffer, even when StateSync paints the alternate screen.
        for (var i = Math.Max(0, state.Rows.Count - _scrollbackBuffer.Capacity); i < state.Rows.Count; i++)
        {
            var (row, links) = Hmp1ScrollbackRowCodec.Decode(state.Rows[i]);
            try
            {
                foreach (var (column, link) in links)
                    row.Cells[column] = row.Cells[column] with
                    {
                        TrackedHyperlink = _trackedObjects.GetOrCreateHyperlink(link.Uri, link.Parameters)
                    };
                _scrollbackBuffer.Push(row.Cells, row.OriginalWidth, row.Timestamp);
            }
            finally
            {
                foreach (var cell in row.Cells)
                    cell.TrackedHyperlink?.Release();
            }
        }
        CollectExpiredTextAnchors();
    }
}
