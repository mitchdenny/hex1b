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

    private Hmp1KeyboardPresentation? _hmp1KeyboardPresentation;
    // A pop that failed when a connection ended; reported once by terminal cleanup.
    private Exception? _hmp1KeyboardRestoreFailure;

    // Remote HMP1 output reaches a byte presentation (a physical host) directly. HMP
    // relays negotiate with their own peers, and cell-impact presentations never see
    // the raw keyboard controls.
    private bool UsesHmp1KeyboardPresentation =>
        _workload is IHmp1TerminalOutputSource &&
        _presentation is not null and not Hmp1PresentationAdapter and not ICellImpactAwarePresentationAdapter;

    private async ValueTask WriteHmp1PresentationAsync(ReadOnlyMemory<byte> data, bool stateSync, CancellationToken ct)
    {
        // Raw-mode entry may still be probing host capabilities. No remote byte may reach
        // the host before it settles, and support must not be latched from a pending probe.
        if (_rawModeEntryTask is not null)
            await _rawModeEntryTask.WaitAsync(ct);
        await _appPresentationLock.WaitAsync(ct);
        try
        {
            if (_disposed)
                return;
            _hmp1KeyboardPresentation ??= new Hmp1KeyboardPresentation(_presentation!.Capabilities.SupportsKittyKeyboard);
            await _hmp1KeyboardPresentation.WriteAsync(_presentation!, data, stateSync, ct);
        }
        finally
        {
            _appPresentationLock.Release();
        }
    }

    // Final terminal cleanup step. A failed pop leaves ownership unknown and is never
    // retried; it, or an earlier failed pop from a connection end, is reported once.
    private async ValueTask CompleteHmp1KeyboardPresentationAsync()
    {
        if (_hmp1KeyboardPresentation is null || _presentation is null)
            return;
        await _appPresentationLock.WaitAsync();
        try
        {
            await PopHmp1KeyboardEntryAsync();
            if (Interlocked.Exchange(ref _hmp1KeyboardRestoreFailure, null) is { } earlier)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(earlier);
        }
        finally
        {
            _appPresentationLock.Release();
        }
    }

    // A connection end must not fault the output pump: the workload may continue with
    // a new connection. The failure is kept for the final cleanup step instead.
    private async ValueTask CompleteHmp1KeyboardPresentationOnDisconnectAsync()
    {
        if (_hmp1KeyboardPresentation is null || _presentation is null)
            return;
        await _appPresentationLock.WaitAsync();
        try
        {
            await PopHmp1KeyboardEntryAsync();
        }
        catch (Exception ex)
        {
            Interlocked.CompareExchange(ref _hmp1KeyboardRestoreFailure, ex, null);
        }
        finally
        {
            _appPresentationLock.Release();
        }
    }

    private async ValueTask PopHmp1KeyboardEntryAsync()
    {
        try
        {
            await _hmp1KeyboardPresentation!.CompleteAsync(_presentation!);
        }
        catch (Exception ex)
        {
            throw new IOException(
                "Failed to pop the HMP1 viewer's Kitty keyboard entry from the host; the host stack may retain it.", ex);
        }
    }

    private async ValueTask<Exception?> TryCompleteHmp1KeyboardPresentationAsync()
    {
        try
        {
            await CompleteHmp1KeyboardPresentationAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async ValueTask WriteAppPresentationAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (UsesHmp1KeyboardPresentation)
        {
            await WriteHmp1PresentationAsync(data, stateSync: false, ct);
            return;
        }
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
                lock (_presentationReadLock)
                {
                    _presentedKeyboardQueryEpoch = queryEpoch;
                    // A read begun before delivery can receive the first genuine reply,
                    // but a completed read must keep its stamp even if its await continuation
                    // has not run. GetResult consumes it under this same lock.
                    if (_presentationReadPending && !_presentationRead.IsCompleted)
                        _presentationReadQueryEpoch = queryEpoch;
                }
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
