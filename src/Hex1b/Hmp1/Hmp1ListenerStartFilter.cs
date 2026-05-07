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
    private readonly List<(Func<CancellationToken, IAsyncEnumerable<Stream>> Source, Func<Stream, Task<Stream>>? Transform)> _streamSources = [];
    private readonly List<Task> _listenerTasks = [];
    private CancellationTokenSource? _listenerCts;

    // Track filter instances per builder so multiple WithHmp1Server calls reuse the same one
    private static readonly ConditionalWeakTable<Hex1bTerminalBuilder, Hmp1ListenerStartFilter> s_filters = new();

    /// <summary>
    /// Gets or creates the filter and adapter for this builder. Multiple calls return the same instance.
    /// </summary>
    /// <remarks>
    /// The first caller's <paramref name="options"/> (if any) wins for
    /// adapter-level event hooks (<see cref="Hmp1ServerOptions.OnClientConnected"/>,
    /// <see cref="Hmp1ServerOptions.OnClientDisconnected"/>,
    /// <see cref="Hmp1ServerOptions.OnResized"/>,
    /// <see cref="Hmp1ServerOptions.OnPrimaryChanged"/>) — these are
    /// adapter-wide, not per-listener. Per-listener stream transforms
    /// are stored alongside each stream source and applied per-client.
    /// </remarks>
    internal static Hmp1ListenerStartFilter GetOrCreate(
        Hex1bTerminalBuilder builder, Hmp1ServerOptions? options, out Hmp1PresentationAdapter adapter)
    {
        if (s_filters.TryGetValue(builder, out var existing))
        {
            adapter = existing._adapter;
            return existing;
        }

        adapter = new Hmp1PresentationAdapter();
        var filter = new Hmp1ListenerStartFilter(adapter);

        // Wire the first-caller options to adapter-wide events. Subsequent
        // WithHmp1*Server calls add additional listeners (and may carry
        // their own per-listener stream transforms) but do not get to
        // re-bind the adapter-wide hooks.
        if (options is not null)
        {
            if (options.OnClientConnected is { } onClientConnected)
            {
                adapter.ClientConnected += (_, e) =>
                {
                    try { onClientConnected(e); } catch { /* observer must not break the server */ }
                };
            }
            if (options.OnClientDisconnected is { } onClientDisconnected)
            {
                adapter.ClientDisconnected += (_, e) =>
                {
                    try { onClientDisconnected(e); } catch { /* observer must not break the server */ }
                };
            }
            if (options.OnResized is { } onResized)
            {
                adapter.Resized += (w, h) =>
                {
                    try { onResized(new Hmp1ServerResizedEventArgs(w, h)); } catch { }
                };
            }
            if (options.OnPrimaryChanged is { } onPrimaryChanged)
            {
                adapter.PrimaryChanged += primaryPeerId =>
                {
                    try { onPrimaryChanged(new Hmp1ServerPrimaryChangedEventArgs(primaryPeerId)); } catch { }
                };
            }
        }

        builder.WithPresentation(adapter);
        builder.AddPresentationFilter(filter);
        s_filters.AddOrUpdate(builder, filter);

        return filter;
    }

    private Hmp1ListenerStartFilter(Hmp1PresentationAdapter adapter)
    {
        _adapter = adapter;
    }

    public void AddStreamSource(
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource,
        Func<Stream, Task<Stream>>? streamTransform = null)
    {
        _streamSources.Add((streamSource, streamTransform));
    }

    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _listenerCts.Token;

        foreach (var (source, transform) in _streamSources)
        {
            _listenerTasks.Add(Task.Run(() => RunListenerAsync(source, transform, token), token));
        }

        return ValueTask.CompletedTask;
    }

    private async Task RunListenerAsync(
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource,
        Func<Stream, Task<Stream>>? streamTransform,
        CancellationToken ct)
    {
        try
        {
            await foreach (var stream in streamSource(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                _ = AddClientSafeAsync(stream, streamTransform, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task AddClientSafeAsync(Stream stream, Func<Stream, Task<Stream>>? transform, CancellationToken ct)
    {
        Stream toAdd = stream;
        try
        {
            if (transform is not null)
            {
                toAdd = await transform(stream).ConfigureAwait(false);
            }

            await _adapter.AddClient(toAdd, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            try { await toAdd.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }

            // If the transform succeeded but AddClient failed, we just disposed the wrapped
            // stream above; the underlying raw stream is owned by the wrapper so no double-dispose.
            // If the transform itself failed, dispose the raw stream.
            if (!ReferenceEquals(toAdd, stream))
            {
                try { await stream.DisposeAsync().ConfigureAwait(false); }
                catch { /* ignore */ }
            }
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
