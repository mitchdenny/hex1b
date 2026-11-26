using System.Text;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private Task? _rawModeEntryTask;
    private readonly SemaphoreSlim _appPresentationLock = new(1, 1);
    private bool _appHostKeyboardPushed;
    private int _presentedKeyboardQueryEpoch;
    // Query epoch of the oldest input still pending in the presentation parser.
    private int _pendingInputQueryEpoch;

    private async ValueTask WriteAppPresentationAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_workload is not Hex1bAppWorkloadAdapter appAdapter)
        {
            await _presentation!.WriteOutputAsync(data, ct);
            return;
        }
        if (_rawModeEntryTask is not null)
            await _rawModeEntryTask.WaitAsync(ct);
        await _appPresentationLock.WaitAsync(ct);
        try
        {
            if (_disposed)
                return;
            // Adapter lifecycle control sequences are each queued as one output item.
            var text = Encoding.UTF8.GetString(data.Span);
            var pushing = text.Contains("\x1b[>31u", StringComparison.Ordinal);
            // Output is pumped in order, so the latest dequeued epoch belongs to this item.
            var queryEpoch = appAdapter.DequeuedKeyboardQueryEpoch;
            await _presentation!.WriteOutputAsync(data, ct);
            if (pushing)
            {
                // Record ownership only after the presentation accepted the push.
                _appHostKeyboardPushed = true;
                // Only input read after the query was delivered may answer it. Input read
                // while the write was pending keeps the older stamp, so re-query: a genuine
                // reply that raced the write is not lost, while one read before delivery
                // cannot confirm. Without request IDs a late reply to an older query that
                // arrives after this point remains indistinguishable from a genuine one.
                Volatile.Write(ref _presentedKeyboardQueryEpoch, queryEpoch);
                await _presentation.WriteOutputAsync("\x1b[?u"u8.ToArray(), ct);
            }
            if (text.Contains("\x1b[<u", StringComparison.Ordinal))
            {
                _appHostKeyboardPushed = false;
                appAdapter.SetInputSourceReportsKeyReleases(false);
            }
        }
        catch
        {
            appAdapter.SetInputSourceReportsKeyReleases(null);
            throw;
        }
        finally
        {
            _appPresentationLock.Release();
        }
    }

    private async ValueTask CompleteAppKeyboardPresentationAsync()
    {
        if (_workload is not Hex1bAppWorkloadAdapter appAdapter)
            return;
        await _appPresentationLock.WaitAsync();
        try
        {
            if (!_appHostKeyboardPushed)
                return;
            // Attempt the pop once: a retry after a partial write could pop a host-owned entry.
            _appHostKeyboardPushed = false;
            await _presentation!.WriteOutputAsync("\x1b[<u"u8.ToArray());
        }
        finally
        {
            appAdapter.SetInputSourceReportsKeyReleases(false);
            _appPresentationLock.Release();
        }
    }
}
