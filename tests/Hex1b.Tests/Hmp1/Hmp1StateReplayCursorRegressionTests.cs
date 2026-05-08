using System.IO.Pipelines;
using System.Text;
using Hex1b;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Regression tests for the HMP1 StateSync replay payload — the bytes a
/// late-joining peer receives so its consumer-side terminal grid matches
/// the producer-side grid that has been running for a while.
/// </summary>
/// <remarks>
/// <para>
/// Original field bug (May 2026): on every fresh viewer connect (and every
/// disconnect+reconnect) the cursor landed one row below the actual prompt
/// row. PowerShell's PSReadLine then accepted the first keystroke at the
/// wrong row, did its full input redraw on the correct row, and left the
/// stray ghost character on the wrong row. The same artefact reproduced
/// on both the WebMuxerDemo browser xterm.js viewer and the WebMuxerDemo
/// CLI viewer — i.e. it was producer-side, not viewer-side.
/// </para>
/// <para>
/// Root cause: <see cref="Hmp1PresentationAdapter"/>'s state-replay path
/// called <c>Hex1bTerminalSnapshot.ToAnsi(...)</c> with the default
/// <c>IncludeTrailingNewline = true</c>. <c>ToAnsi</c> first emits a CSI
/// cursor-position sequence to park the cursor at the workload's actual
/// (col, row), then appends a trailing <c>\r\n</c>. The trailing newline
/// scrolls the cursor down one row, leaving every PSReadLine-style
/// readline consumer in the wrong place until its next full redraw.
/// </para>
/// <para>
/// Fix: pass <c>IncludeTrailingNewline = false</c> for the state-replay
/// payload (the option exists for FILE writes — POSIX EOF newline — and
/// is actively harmful for an over-the-wire snapshot whose final bytes
/// place the cursor).
/// </para>
/// </remarks>
public class Hmp1StateReplayCursorRegressionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StateSync_DoesNotEndWithTrailingNewline_SoCursorStaysWherePainterPutIt()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);

        // Build a real terminal so the snapshot path (the bugged branch) is
        // exercised. The workload is a no-op stub — ApplyTokens lets the
        // test inject known producer output directly into the grid so
        // ToAnsi has a non-trivial cell payload AND a non-default cursor
        // position to encode without needing a real PTY.
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless()
            .Build();

        // Wire the presentation adapter to the terminal manually (the
        // WithHmp1Server builder extension does this internally; we go
        // through the public ITerminalLifecycleAwarePresentationAdapter
        // surface so the test doesn't depend on builder ordering).
        server.TerminalCreated(terminal);

        // Fill some cells AND park the cursor at a known non-zero, non-EOL
        // position. "PS C:\> " is 8 chars on row 0 → cursor lands at
        // (col=8, row=0). Anything that keeps the cursor away from
        // (col=0, row=23) — i.e. away from the trivial "blank screen,
        // cursor in last cell" case — is enough to expose the bug.
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("PS C:\\> "));

        var (s1, s2) = CreateFullDuplexPair();

        using var cts = new CancellationTokenSource(TestTimeout);
        var addClientTask = server.AddClient(s1, cts.Token);

        // Speak HMP1 directly off the wire so we can observe the raw
        // StateSync payload byte-for-byte instead of letting
        // Hmp1WorkloadAdapter parse and apply it.
        await Hmp1Protocol.WriteClientHelloAsync(s2, displayName: "regression", defaultRole: null, cts.Token);

        var helloFrame = await Hmp1Protocol.ReadFrameAsync(s2, cts.Token)
            ?? throw new Xunit.Sdk.XunitException("Server closed the stream before sending Hello.");
        Assert.Equal(Hmp1FrameType.Hello, helloFrame.Type);

        var stateSyncFrame = await Hmp1Protocol.ReadFrameAsync(s2, cts.Token)
            ?? throw new Xunit.Sdk.XunitException("Server closed the stream before sending StateSync.");
        Assert.Equal(Hmp1FrameType.StateSync, stateSyncFrame.Type);

        // Drain the AddClient task so any post-handshake observer doesn't
        // race with test teardown.
        var handle = await addClientTask;
        await using var handleDispose = handle;

        var payload = stateSyncFrame.Payload;
        Assert.False(payload.IsEmpty, "StateSync payload should carry the screen snapshot.");

        // Core regression assertion: a trailing \n (or \r\n) here would
        // scroll the cursor down one row past wherever the cursor-position
        // sequence in the ToAnsi output just placed it. Encode the expected
        // last byte explicitly so a future tweak that re-introduces
        // IncludeTrailingNewline = true fails this loud and clear.
        var lastByte = payload.Span[^1];
        Assert.True(
            lastByte != (byte)'\n',
            $"StateSync payload ended with LF (0x0A). The cursor-position sequence emitted by ToAnsi "
            + "would be invalidated by a trailing newline (cursor scrolls down one row). "
            + "If you re-enabled IncludeTrailingNewline = true in Hmp1PresentationAdapter, "
            + "PSReadLine and other readline-style consumers will mis-place the first "
            + "keystroke after every fresh peer connect.");

        // Stronger shape assertion: the very last bytes of a non-empty,
        // non-alt-screen, non-mode-changed snapshot must be a CSI cursor-
        // positioning sequence emitted by ToAnsi's IncludeCursorPosition
        // branch. The exact form is `\x1b[{n}G` (column-only when we're on
        // the bottom row already) or `\x1b[{m}A\x1b[{n}G`. In either case
        // the trailing token is `G`.
        Assert.Equal((byte)'G', lastByte);
    }

    private sealed class NullWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (Stream S1, Stream S2) CreateFullDuplexPair()
    {
        var p12 = new Pipe();
        var p21 = new Pipe();
        return (
            new DuplexPipeStream(p21.Reader.AsStream(), p12.Writer.AsStream()),
            new DuplexPipeStream(p12.Reader.AsStream(), p21.Writer.AsStream()));
    }

    private sealed class DuplexPipeStream(Stream readStream, Stream writeStream) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => writeStream.Flush();
        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => readStream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => readStream.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => writeStream.WriteAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { readStream.Dispose(); } catch { }
                try { writeStream.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
