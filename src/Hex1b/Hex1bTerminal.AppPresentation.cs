using System.Text;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private Task? _rawModeEntryTask;
    private readonly SemaphoreSlim _appPresentationLock = new(1, 1);
    private bool _appHostKeyboardPushed;

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
            await _presentation!.WriteOutputAsync(data, ct);
            // Adapter lifecycle control sequences are each queued as one output item.
            // Record ownership only after the presentation accepted the push.
            var text = Encoding.UTF8.GetString(data.Span);
            if (text.Contains("\x1b[>31u", StringComparison.Ordinal))
                _appHostKeyboardPushed = true;
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
            await _presentation!.WriteOutputAsync("\x1b[<u"u8.ToArray());
            _appHostKeyboardPushed = false;
        }
        finally
        {
            appAdapter.SetInputSourceReportsKeyReleases(false);
            _appPresentationLock.Release();
        }
    }
}
