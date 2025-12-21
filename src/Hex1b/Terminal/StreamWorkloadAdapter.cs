using System.Threading.Channels;

namespace Hex1b.Terminal;

/// <summary>
/// A simple workload adapter that wraps input and output streams.
/// Useful for testing or connecting to arbitrary byte streams.
/// </summary>
/// <remarks>
/// <para>
/// This adapter treats the streams from the perspective of the workload:
/// <list type="bullet">
///   <item>OutputStream: Where the workload writes its output (terminal reads from here)</item>
///   <item>InputStream: Where the workload reads its input (terminal writes here)</item>
/// </list>
/// </para>
/// <para>
/// For testing, you can use <see cref="MemoryStream"/> or pipe streams.
/// The terminal will read from outputStream and write to inputStream.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create streams for bidirectional communication
/// var outputPipe = new Pipe();
/// var inputPipe = new Pipe();
/// 
/// var workload = new StreamWorkloadAdapter(
///     outputReader: outputPipe.Reader.AsStream(),  // terminal reads workload output
///     inputWriter: inputPipe.Writer.AsStream());   // terminal writes workload input
/// 
/// // Now write to outputPipe.Writer to simulate workload output
/// // Read from inputPipe.Reader to see what terminal sent as input
/// </code>
/// </example>
public sealed class StreamWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Stream _outputReader;
    private readonly Stream _inputWriter;
    private readonly byte[] _readBuffer = new byte[4096];
    private bool _disposed;

    /// <summary>
    /// Creates a new stream workload adapter.
    /// </summary>
    /// <param name="outputReader">Stream to read workload output from (what the terminal displays).</param>
    /// <param name="inputWriter">Stream to write input to (keystrokes to workload).</param>
    public StreamWorkloadAdapter(Stream outputReader, Stream inputWriter)
    {
        _outputReader = outputReader ?? throw new ArgumentNullException(nameof(outputReader));
        _inputWriter = inputWriter ?? throw new ArgumentNullException(nameof(inputWriter));
    }

    /// <summary>
    /// Creates a headless workload adapter with in-memory channels for testing.
    /// </summary>
    /// <param name="width">Terminal width.</param>
    /// <param name="height">Terminal height.</param>
    public static StreamWorkloadAdapter CreateHeadless(int width = 80, int height = 24)
    {
        // For headless mode, we use memory streams that can be written to externally
        var outputStream = new MemoryStream();
        var inputStream = new MemoryStream();
        return new StreamWorkloadAdapter(outputStream, inputStream)
        {
            Width = width,
            Height = height
        };
    }

    /// <summary>
    /// Terminal width (for informational purposes).
    /// </summary>
    public int Width { get; set; } = 80;

    /// <summary>
    /// Terminal height (for informational purposes).
    /// </summary>
    public int Height { get; set; } = 24;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        try
        {
            var bytesRead = await _outputReader.ReadAsync(_readBuffer, ct);
            if (bytesRead == 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }
            return new ReadOnlyMemory<byte>(_readBuffer, 0, bytesRead);
        }
        catch (ObjectDisposedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return;

        try
        {
            await _inputWriter.WriteAsync(data, ct);
            await _inputWriter.FlushAsync(ct);
        }
        catch (ObjectDisposedException)
        {
            // Stream was closed
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        Width = width;
        Height = height;
        // For raw streams, we don't send SIGWINCH or similar
        // A real PTY adapter would handle this
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Signals that the workload has disconnected.
    /// </summary>
    public void SignalDisconnected()
    {
        Disconnected?.Invoke();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        // Don't dispose the streams - they're owned by the caller
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }
}
