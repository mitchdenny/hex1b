namespace WebTerminalDemo;

internal sealed class TerminalView(TerminalInstance instance, string id, Action onClosed) : IDisposable
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<BrowserCloseRequest> _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _closing;
    private int _disposed;

    public TerminalInstance Instance { get; } = instance;
    public string Id { get; } = id;
    public Task Completion => _completion.Task;
    public Task<BrowserCloseRequest> Failure => _failure.Task;

    public int RequestFailure(BrowserCloseRequest failure)
    {
        if (Interlocked.Exchange(ref _closing, 1) != 0)
            return 409;
        _failure.TrySetResult(failure);
        return 202;
    }

    public void BeginClosing() => Interlocked.Exchange(ref _closing, 1);

    public void Dispose()
    {
        BeginClosing();
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Instance.CloseView(this);
        onClosed();
        _completion.TrySetResult();
    }
}
