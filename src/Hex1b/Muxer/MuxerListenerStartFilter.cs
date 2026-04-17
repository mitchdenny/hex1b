using Hex1b.Tokens;

namespace Hex1b.Muxer;

/// <summary>
/// A presentation filter that starts the muxer listener when the terminal session begins.
/// </summary>
internal sealed class MuxerListenerStartFilter : IHex1bTerminalPresentationFilter
{
    private readonly MuxerPresentationAdapter _adapter;
    private readonly Func<MuxerPresentationAdapter, CancellationToken, Task> _listenerFactory;
    private Task? _listenerTask;
    private CancellationTokenSource? _listenerCts;

    public MuxerListenerStartFilter(
        MuxerPresentationAdapter adapter,
        Func<MuxerPresentationAdapter, CancellationToken, Task> listenerFactory)
    {
        _adapter = adapter;
        _listenerFactory = listenerFactory;
    }

    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listenerTask = Task.Run(() => _listenerFactory(_adapter, _listenerCts.Token), _listenerCts.Token);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
        IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(
            appliedTokens.Select(t => t.Token).ToList());
    }

    public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public async ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        if (_listenerCts is not null)
        {
            await _listenerCts.CancelAsync().ConfigureAwait(false);
            _listenerCts.Dispose();
        }

        if (_listenerTask is not null)
        {
            try { await _listenerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }
}
