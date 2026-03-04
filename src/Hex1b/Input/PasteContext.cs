using System.Text;
using System.Threading.Channels;

namespace Hex1b.Input;

/// <summary>
/// Provides streaming access to bracketed paste data.
/// Created when a paste start marker (ESC[200~) is detected,
/// data is written as it arrives, and completed when the end marker (ESC[201~) is received.
/// </summary>
public sealed class PasteContext : IAsyncDisposable
{
    private readonly Channel<string> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource _completedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isCancelled;
    private volatile bool _isCompleted;
    private long _totalCharactersWritten;

    /// <summary>
    /// Creates a new PasteContext with a bounded channel for streaming paste data.
    /// </summary>
    /// <param name="invalidate">Action to request a UI re-render (safe to call from any thread).</param>
    /// <param name="boundedCapacity">Maximum number of buffered chunks before backpressure is applied.</param>
    public PasteContext(Action? invalidate = null, int boundedCapacity = 64)
    {
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(boundedCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
        _cts = new CancellationTokenSource();
        Invalidate = invalidate ?? (() => { });
    }

    /// <summary>
    /// Request a UI re-render. Safe to call from any thread.
    /// Call this from your paste handler when you have UI updates to show (e.g., progress).
    /// </summary>
    public Action Invalidate { get; }

    /// <summary>
    /// A task that completes when the paste end marker (ESC[201~) is received
    /// or the paste is cancelled.
    /// </summary>
    public Task Completed => _completedTcs.Task;

    /// <summary>
    /// Whether the paste has finished receiving data (end marker received).
    /// </summary>
    public bool IsCompleted => _isCompleted;

    /// <summary>
    /// Whether the paste was cancelled before the end marker was received.
    /// </summary>
    public bool IsCancelled => _isCancelled;

    /// <summary>
    /// Cancellation token that is signaled when the paste is cancelled
    /// (by user Escape, timeout, size limit, or programmatic Cancel() call).
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Total number of characters written to the paste context so far.
    /// </summary>
    public long TotalCharactersWritten => Interlocked.Read(ref _totalCharactersWritten);

    /// <summary>
    /// Cancel the paste. The handler's async enumerables will stop yielding.
    /// Remaining paste data from the terminal is drained and discarded until ESC[201~.
    /// </summary>
    public void Cancel()
    {
        if (_isCancelled || _isCompleted) return;
        _isCancelled = true;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _completedTcs.TrySetResult();
    }

    // === Writer API (called by Hex1bTerminal pump thread) ===

    /// <summary>
    /// Write a chunk of paste data. Called by the terminal pump as text arrives.
    /// Returns false if the context has been cancelled or completed.
    /// </summary>
    internal async ValueTask<bool> WriteAsync(string text, CancellationToken ct = default)
    {
        if (_isCancelled || _isCompleted) return false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            await _channel.Writer.WriteAsync(text, linked.Token);
            Interlocked.Add(ref _totalCharactersWritten, text.Length);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Try to write a chunk synchronously (non-blocking).
    /// Returns false if the channel is full, cancelled, or completed.
    /// </summary>
    internal bool TryWrite(string text)
    {
        if (_isCancelled || _isCompleted) return false;
        var result = _channel.Writer.TryWrite(text);
        if (result)
            Interlocked.Add(ref _totalCharactersWritten, text.Length);
        return result;
    }

    /// <summary>
    /// Signal that the paste end marker (ESC[201~) was received.
    /// </summary>
    internal void Complete()
    {
        if (_isCompleted) return;
        _isCompleted = true;
        _channel.Writer.TryComplete();
        _completedTcs.TrySetResult();
    }

    // === Reader API (called by paste handlers) ===

    /// <summary>
    /// Read the entire paste content as a string. Best for small pastes.
    /// For large pastes, prefer <see cref="ReadChunksAsync"/> or <see cref="ReadLinesAsync"/>.
    /// </summary>
    /// <param name="maxBytes">Maximum number of characters to read. Throws if exceeded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete paste text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when paste exceeds maxBytes.</exception>
    public async Task<string> ReadToEndAsync(int maxBytes = 4 * 1024 * 1024, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var sb = new StringBuilder();

        try
        {
            await foreach (var chunk in _channel.Reader.ReadAllAsync(linked.Token))
            {
                sb.Append(chunk);
                if (sb.Length > maxBytes)
                {
                    throw new InvalidOperationException(
                        $"Paste content exceeds maximum size of {maxBytes} characters. " +
                        $"Use ReadChunksAsync() or CopyToAsync() for large pastes.");
                }
            }
        }
        catch (OperationCanceledException) when (_isCancelled)
        {
            // Paste was cancelled — return what we have so far
        }

        return sb.ToString();
    }

    /// <summary>
    /// Read paste data chunk-by-chunk as it arrives from the terminal.
    /// Each chunk corresponds to one or more tokens from a single terminal read.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<string> ReadChunksAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        var reader = _channel.Reader;
        while (true)
        {
            string chunk;
            try
            {
                if (!await reader.WaitToReadAsync(linked.Token))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            while (reader.TryRead(out chunk!))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Read paste data line-by-line as lines become available.
    /// Handles line endings: \n, \r\n, \r.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<string> ReadLinesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var buffer = new StringBuilder();

        await foreach (var chunk in _channel.Reader.ReadAllAsync(linked.Token))
        {
            for (int i = 0; i < chunk.Length; i++)
            {
                char c = chunk[i];
                if (c == '\r')
                {
                    yield return buffer.ToString();
                    buffer.Clear();
                    // Skip \n following \r
                    if (i + 1 < chunk.Length && chunk[i + 1] == '\n')
                        i++;
                }
                else if (c == '\n')
                {
                    yield return buffer.ToString();
                    buffer.Clear();
                }
                else
                {
                    buffer.Append(c);
                }
            }
        }

        // Yield remaining content if any
        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }

    /// <summary>
    /// Copy all paste data to a destination stream as it arrives.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="encoding">Text encoding to use. Defaults to UTF-8.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task CopyToAsync(Stream destination, Encoding? encoding = null, CancellationToken ct = default)
    {
        encoding ??= Encoding.UTF8;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        await foreach (var chunk in _channel.Reader.ReadAllAsync(linked.Token))
        {
            var bytes = encoding.GetBytes(chunk);
            await destination.WriteAsync(bytes, linked.Token);
        }

        await destination.FlushAsync(linked.Token);
    }

    /// <summary>
    /// Save all paste data to a file as it arrives.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="encoding">Text encoding to use. Defaults to UTF-8.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveToFileAsync(string path, Encoding? encoding = null, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 
            bufferSize: 8192, useAsync: true);
        await CopyToAsync(stream, encoding, ct);
    }

    /// <summary>
    /// Disposes the PasteContext, cancelling any pending operations.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}
