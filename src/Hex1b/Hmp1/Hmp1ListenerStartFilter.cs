using System.Runtime.CompilerServices;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// A presentation filter that starts muxer listeners when the terminal session begins.
/// Supports multiple stream sources (transports) feeding into a single adapter.
/// </summary>
internal sealed class Hmp1ListenerStartFilter : IHex1bTerminalPresentationFilter
{
    private readonly Hmp1PresentationAdapter _adapter;
    private readonly List<Func<CancellationToken, IAsyncEnumerable<Stream>>> _streamSources = [];
    private readonly List<Task> _listenerTasks = [];
    private CancellationTokenSource? _listenerCts;

    // Track filter instances per builder so multiple WithHmp1Server calls reuse the same one
    private static readonly ConditionalWeakTable<Hex1bTerminalBuilder, Hmp1ListenerStartFilter> s_filters = new();

    /// <summary>
    /// Gets or creates the filter and adapter for this builder. Multiple calls return the same instance.
    /// </summary>
    internal static Hmp1ListenerStartFilter GetOrCreate(
        Hex1bTerminalBuilder builder, out Hmp1PresentationAdapter adapter)
    {
        if (s_filters.TryGetValue(builder, out var existing))
        {
            adapter = existing._adapter;
            return existing;
        }

        adapter = new Hmp1PresentationAdapter();
        var filter = new Hmp1ListenerStartFilter(adapter);

        builder.WithPresentation(adapter);
        builder.AddPresentationFilter(filter);
        s_filters.AddOrUpdate(builder, filter);

        return filter;
    }

    private Hmp1ListenerStartFilter(Hmp1PresentationAdapter adapter)
    {
        _adapter = adapter;
    }

    public void AddStreamSource(Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource)
    {
        _streamSources.Add(streamSource);
    }

    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _listenerCts.Token;

        foreach (var source in _streamSources)
        {
            _listenerTasks.Add(Task.Run(() => RunListenerAsync(source, token), token));
        }

        return ValueTask.CompletedTask;
    }

    private async Task RunListenerAsync(
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource, CancellationToken ct)
    {
        try
        {
            await foreach (var stream in streamSource(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                _ = AddClientSafeAsync(stream, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task AddClientSafeAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            await _adapter.AddClient(stream, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            try { await stream.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }
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

        foreach (var task in _listenerTasks)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }
}
