using Hex1b.Input;

namespace Hex1b.Tests;

[TestClass]
public class PasteContextTests
{
    [TestMethod]
    public async Task ReadToEnd_SmallText_ReturnsFullText()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("hello", text);
    }

    [TestMethod]
    public async Task ReadToEnd_EmptyPaste_ReturnsEmptyString()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public async Task ReadToEnd_SingleChar_ReturnsSingleChar()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("x");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("x", text);
    }

    [TestMethod]
    public async Task ReadToEnd_MultiLine_PreservesNewlines()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("line1\nline2\r\nline3\ttab");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("line1\nline2\r\nline3\ttab", text);
    }

    [TestMethod]
    public async Task ReadToEnd_UnicodeEmoji_PreservesContent()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello 🌍 世界 🎉");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("hello 🌍 世界 🎉", text);
    }

    [TestMethod]
    public async Task ReadToEnd_ExceedsMaxBytes_Throws()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite(new string('a', 100));
        ctx.Complete();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.ReadToEndAsync(maxCharacters: 50));
    }

    [TestMethod]
    public async Task ReadToEnd_MultipleChunks_ConcatenatesAll()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello ");
        ctx.TryWrite("world");
        ctx.TryWrite("!");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.AreEqual("hello world!", text);
    }

    [TestMethod]
    public async Task ReadChunks_ReceivesIncrementally()
    {
        await using var ctx = new PasteContext();
        var chunks = new List<string>();

        // Write chunks then complete
        ctx.TryWrite("chunk1");
        ctx.TryWrite("chunk2");
        ctx.TryWrite("chunk3");
        ctx.Complete();

        await foreach (var chunk in ctx.ReadChunksAsync())
        {
            chunks.Add(chunk);
        }

        Assert.AreEqual(3, chunks.Count);
        Assert.AreEqual("chunk1", chunks[0]);
        Assert.AreEqual("chunk2", chunks[1]);
        Assert.AreEqual("chunk3", chunks[2]);
    }

    [TestMethod]
    public async Task ReadLines_SplitsCorrectly()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("line1\nline2\nline3");
        ctx.Complete();

        var lines = new List<string>();
        await foreach (var line in ctx.ReadLinesAsync())
        {
            lines.Add(line);
        }

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("line1", lines[0]);
        Assert.AreEqual("line2", lines[1]);
        Assert.AreEqual("line3", lines[2]);
    }

    [TestMethod]
    public async Task ReadLines_HandlesCarriageReturnLineFeed()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("line1\r\nline2\rline3");
        ctx.Complete();

        var lines = new List<string>();
        await foreach (var line in ctx.ReadLinesAsync())
        {
            lines.Add(line);
        }

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("line1", lines[0]);
        Assert.AreEqual("line2", lines[1]);
        Assert.AreEqual("line3", lines[2]);
    }

    [TestMethod]
    public async Task CopyToAsync_WritesToStream()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello world");
        ctx.Complete();

        using var ms = new MemoryStream();
        await ctx.CopyToAsync(ms);

        ms.Position = 0;
        using var reader = new StreamReader(ms);
        var text = await reader.ReadToEndAsync();
        Assert.AreEqual("hello world", text);
    }

    [TestMethod]
    public async Task SaveToFile_CreatesFile()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await using var ctx = new PasteContext();
            ctx.TryWrite("file content");
            ctx.Complete();

            await ctx.SaveToFileAsync(tempPath);

            var content = await File.ReadAllTextAsync(tempPath);
            Assert.AreEqual("file content", content);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [TestMethod]
    public async Task Cancel_StopsReading()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("before");
        ctx.Cancel();

        var chunks = new List<string>();
        await foreach (var chunk in ctx.ReadChunksAsync())
        {
            chunks.Add(chunk);
        }

        // Should get at most the chunk that was written before cancel
        Assert.IsTrue(chunks.Count <= 1);
    }

    [TestMethod]
    public void Cancel_SignalsCancellationToken()
    {
        var ctx = new PasteContext();
        try
        {
            Assert.IsFalse(ctx.CancellationToken.IsCancellationRequested);

            ctx.Cancel();

            Assert.IsTrue(ctx.CancellationToken.IsCancellationRequested);
            Assert.IsTrue(ctx.IsCancelled);
        }
        finally
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [TestMethod]
    public async Task Completed_SignaledOnComplete()
    {
        await using var ctx = new PasteContext();
        Assert.IsFalse(ctx.IsCompleted);

        ctx.Complete();

        await ctx.Completed; // Should not hang
        Assert.IsTrue(ctx.IsCompleted);
    }

    [TestMethod]
    public async Task Completed_SignaledOnCancel()
    {
        await using var ctx = new PasteContext();

        ctx.Cancel();

        await ctx.Completed; // Should not hang
        Assert.IsTrue(ctx.IsCancelled);
    }

    [TestMethod]
    public async Task Dispose_CompletesContext()
    {
        var ctx = new PasteContext();
        await ctx.DisposeAsync();

        Assert.IsTrue(ctx.IsCancelled);
    }

    [TestMethod]
    public async Task WriteAsync_AfterCancel_ReturnsFalse()
    {
        await using var ctx = new PasteContext();
        ctx.Cancel();

        var written = await ctx.WriteAsync("should not write");
        Assert.IsFalse(written);
    }

    [TestMethod]
    public async Task WriteAsync_AfterComplete_ReturnsFalse()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();

        var written = await ctx.WriteAsync("should not write");
        Assert.IsFalse(written);
    }

    [TestMethod]
    public void TryWrite_AfterCancel_ReturnsFalse()
    {
        var ctx = new PasteContext();
        try
        {
            ctx.Cancel();
            Assert.IsFalse(ctx.TryWrite("should not write"));
        }
        finally
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [TestMethod]
    public async Task Cancel_Idempotent()
    {
        await using var ctx = new PasteContext();
        ctx.Cancel();
        ctx.Cancel(); // Should not throw

        Assert.IsTrue(ctx.IsCancelled);
    }

    [TestMethod]
    public async Task Complete_Idempotent()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();
        ctx.Complete(); // Should not throw

        Assert.IsTrue(ctx.IsCompleted);
    }

    [TestMethod]
    public async Task Invalidate_CalledWhenProvided()
    {
        int invalidateCount = 0;
        await using var ctx = new PasteContext(invalidate: () => invalidateCount++);

        ctx.Invalidate();
        ctx.Invalidate();

        Assert.AreEqual(2, invalidateCount);
    }

    [TestMethod]
    public async Task ReadToEnd_CancelledDuringRead_ThrowsOrReturnsPartial()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello");

        // Cancel after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            ctx.Cancel();
        });

        // ReadToEndAsync should either throw OperationCanceledException or return partial data
        try
        {
            var text = await ctx.ReadToEndAsync();
            // If it returns, it should have the data written before cancel
            Assert.AreEqual("hello", text);
        }
        catch (OperationCanceledException)
        {
            // Also acceptable
        }
    }

    [TestMethod]
    public async Task ReadChunks_StreamsAsDataArrives()
    {
        await using var ctx = new PasteContext();
        var receivedChunks = new List<string>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var chunk in ctx.ReadChunksAsync())
            {
                receivedChunks.Add(chunk);
            }
        });

        // Write chunks with small delays to simulate streaming
        await ctx.WriteAsync("chunk1");
        await Task.Delay(10);
        await ctx.WriteAsync("chunk2");
        await Task.Delay(10);
        ctx.Complete();

        await readTask;

        Assert.AreEqual(2, receivedChunks.Count);
        Assert.AreEqual("chunk1", receivedChunks[0]);
        Assert.AreEqual("chunk2", receivedChunks[1]);
    }

    [TestMethod]
    public async Task ReadLines_AcrossChunks_MergesCorrectly()
    {
        await using var ctx = new PasteContext();
        // Line boundary split across chunks
        ctx.TryWrite("hel");
        ctx.TryWrite("lo\nwor");
        ctx.TryWrite("ld");
        ctx.Complete();

        var lines = new List<string>();
        await foreach (var line in ctx.ReadLinesAsync())
        {
            lines.Add(line);
        }

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual("hello", lines[0]);
        Assert.AreEqual("world", lines[1]);
    }
}
