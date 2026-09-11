using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests;

[TestClass]
public class TapeParserInputTests
{
    [TestMethod]
    public async Task ParseAsync_StreamCurrentPosition_ConsumesRemainderWithoutClosing()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ignored Type '😀' Enter"));
        stream.Position = 8;
        var document = await new TapeParser().ParseAsync(stream, "borrowed", TestContext.CancellationToken);
        Assert.AreEqual(2, document.Commands.Count);
        Assert.AreEqual("😀", TestSeq.IsType<TapeTypeCommand>(document.Commands[0]).Text);
        Assert.AreEqual("borrowed", document.SourceName);
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public async Task ParseAsync_NonSeekableStream_ParsesWithoutSeeking()
    {
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes("Type 'non-seekable'"));
        var document = await new TapeParser().ParseAsync(stream, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual("non-seekable", TestSeq.IsType<TapeTypeCommand>(TestSeq.Single(document.Commands)).Text);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public async Task ParseAsync_TextReader_LeavesBorrowedReaderOpen()
    {
        using var reader = new StringReader("Type 'reader'");
        var document = await new TapeParser().ParseAsync(reader, "reader-source", TestContext.CancellationToken);
        Assert.AreEqual("reader-source", document.SourceName);
        Assert.AreEqual(-1, reader.Read());
    }

    [TestMethod]
    public async Task ParseAsync_InvalidContent_LeavesBothBorrowedInputsOpen()
    {
        var parser = new TapeParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Unknown"));
        using var reader = new StringReader("Unknown");
        await Assert.ThrowsExactlyAsync<TapeParseException>(() => parser.ParseAsync(stream, cancellationToken: TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<TapeParseException>(() => parser.ParseAsync(reader, cancellationToken: TestContext.CancellationToken));
        Assert.IsTrue(stream.CanRead);
        Assert.AreEqual(-1, reader.Read());
    }

    [TestMethod]
    public async Task ParseAsync_PreCanceled_LeavesBorrowedInputsUnreadAndOpen()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var parser = new TapeParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hide"));
        using var reader = new StringReader("Hide");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => parser.ParseAsync(stream, cancellationToken: cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => parser.ParseAsync(reader, cancellationToken: cancellation.Token));
        Assert.AreEqual(0, stream.Position);
        Assert.AreEqual('H', (char)reader.Read());
    }

    [TestMethod]
    public async Task ParseAsync_CanceledDuringRead_LeavesBorrowedReaderOpen()
    {
        using var cancellation = new CancellationTokenSource();
        using var reader = new CancelingReader(cancellation);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new TapeParser().ParseAsync(reader, cancellationToken: cancellation.Token));
        Assert.IsFalse(reader.Disposed);
    }

    [TestMethod]
    public async Task ParseAsync_CanceledDuringRecognition_StopsBeforeLaterCommands()
    {
        using var cancellation = new CancellationTokenSource();
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Cancel", _ =>
        {
            cancellation.Cancel();
            return context => TapeCommandResult.Accept(
                new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        });
        var parser = new TapeParser(options);
        using var reader = new StringReader("Cancel Unknown");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => parser.ParseAsync(reader, cancellationToken: cancellation.Token));
        Assert.AreEqual(-1, reader.Read());
    }

    [TestMethod]
    public async Task ParseAsync_FileInfo_LabelsSourceAndClosesOwnedFile()
    {
        var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), $".tape-parser-{Guid.NewGuid():N}.tape"));
        try
        {
            await File.WriteAllTextAsync(file.FullName, "Type 'file'\nSource 'not-opened.tape'", new UTF8Encoding(false), TestContext.CancellationToken);
            var document = await new TapeParser().ParseAsync(file, TestContext.CancellationToken);
            Assert.AreEqual(file.FullName, document.SourceName);
            Assert.AreEqual(file.FullName, document.Commands[0].Span.SourceName);
            Assert.AreEqual(2, document.Commands.Count);
            using var exclusive = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.IsTrue(exclusive.CanWrite);
        }
        finally
        {
            file.Delete();
        }
    }

    [TestMethod]
    public async Task ParseAsync_FileAndStream_UseIdenticalUtf8Decoding()
    {
        var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), $".tape-parser-{Guid.NewGuid():N}.tape"));
        try
        {
            var bytes = Encoding.UTF8.GetBytes("Type 'café😀'");
            await File.WriteAllBytesAsync(file.FullName, bytes, TestContext.CancellationToken);
            using var stream = new MemoryStream(bytes);
            var parser = new TapeParser();
            var fromFile = await parser.ParseAsync(file, TestContext.CancellationToken);
            var fromStream = await parser.ParseAsync(stream, cancellationToken: TestContext.CancellationToken);
            Assert.AreEqual(
                TestSeq.IsType<TapeTypeCommand>(fromFile.Commands[0]).Text,
                TestSeq.IsType<TapeTypeCommand>(fromStream.Commands[0]).Text);
        }
        finally
        {
            file.Delete();
        }
    }

    [TestMethod]
    public async Task ParseAsync_IoError_PropagatesWithoutSyntaxWrapping()
    {
        using var reader = new FailingReader();
        await Assert.ThrowsExactlyAsync<IOException>(() => new TapeParser().ParseAsync(reader, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ParseAsync_Utf8Bom_PreservesUpstreamIllegalToken()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Hide")).ToArray();
        using var stream = new MemoryStream(bytes);
        await Assert.ThrowsExactlyAsync<TapeParseException>(() => new TapeParser().ParseAsync(stream, cancellationToken: TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class NonSeekableStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    private sealed class CancelingReader(CancellationTokenSource cancellation) : StringReader("Hide")
    {
        internal bool Disposed { get; private set; }
        public override Task<string> ReadToEndAsync(CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromResult("Hide");
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingReader : StringReader
    {
        internal FailingReader() : base("") { }
        public override Task<string> ReadToEndAsync(CancellationToken cancellationToken) => throw new IOException("reader failed");
    }

}
