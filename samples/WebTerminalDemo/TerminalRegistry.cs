using Microsoft.Extensions.Logging;

namespace WebTerminalDemo;

internal sealed class TerminalRegistry(DemoTapeCatalog tapeCatalog, ILogger logger, CancellationToken applicationStopping) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TerminalInstance> _instances = [];
    private int _viewCount;
    private bool _disposed;

    public TerminalInstanceInfo[] List()
    {
        lock (_gate)
            return _instances.Values.Where(instance => !instance.IsStopping).Select(instance => instance.GetInfo()).ToArray();
    }

    public TerminalInstanceInfo? Create(CreateTerminalRequest request)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed || applicationStopping.IsCancellationRequested, this);
            if (_instances.Count >= 4)
                return null;
            var instance = new TerminalInstance(request, tapeCatalog, logger, applicationStopping, OnCompleted);
            _instances.Add(instance.Id, instance);
            instance.Start();
            return instance.GetInfo();
        }
    }

    public (TerminalView? View, int Status) TryOpenView(string id, string? viewId = null)
    {
        lock (_gate)
        {
            if (_disposed || !_instances.TryGetValue(id, out var instance) || instance.IsStopping)
                return (null, 404);
            if (_viewCount >= 8)
                return (null, 429);
            var (view, status) = instance.TryOpenView(viewId, OnViewClosed);
            if (view is null)
                return (null, status);
            _viewCount++;
            return (view, 200);
        }
    }

    public int RequestViewFailure(string id, string viewId, BrowserCloseRequest failure)
    {
        lock (_gate)
            return !_instances.TryGetValue(id, out var instance) ? 404 : instance.RequestViewFailure(viewId, failure);
    }

    public int UpdateControls(string id, TerminalControlsRequest request)
    {
        lock (_gate)
            return !_instances.TryGetValue(id, out var instance) || instance.IsStopping
                ? 404 : instance.UpdateControls(request);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        TerminalInstance? instance;
        lock (_gate)
            _instances.TryGetValue(id, out instance);
        if (instance is null)
            return false;
        await instance.StopAsync();
        return true;
    }

    public int StartTape(string id, string tapeId)
    {
        lock (_gate)
            return !_instances.TryGetValue(id, out var instance) || instance.IsStopping
                ? 404 : instance.StartTape(tapeId);
    }

    public int CancelTape(string id)
    {
        lock (_gate)
            return !_instances.TryGetValue(id, out var instance) || instance.IsStopping
                ? 404 : instance.CancelTape();
    }

    private void OnViewClosed()
    {
        lock (_gate)
            _viewCount--;
    }

    private void OnCompleted(TerminalInstance instance)
    {
        lock (_gate)
            _instances.Remove(instance.Id);
    }

    public async ValueTask DisposeAsync()
    {
        TerminalInstance[] instances;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            instances = _instances.Values.ToArray();
        }
        await Task.WhenAll(instances.Select(instance => instance.StopAsync()));
    }
}
