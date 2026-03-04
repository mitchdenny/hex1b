using Hex1b.Input;

namespace Hex1b.Tests;

public class PasteContextTests
{
    [Fact]
    public async Task ReadToEnd_SmallText_ReturnsFullText()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task ReadToEnd_EmptyPaste_ReturnsEmptyString()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("", text);
    }

    [Fact]
    public async Task ReadToEnd_SingleChar_ReturnsSingleChar()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("x");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("x", text);
    }

    [Fact]
    public async Task ReadToEnd_MultiLine_PreservesNewlines()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("line1\nline2\r\nline3\ttab");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("line1\nline2\r\nline3\ttab", text);
    }

    [Fact]
    public async Task ReadToEnd_UnicodeEmoji_PreservesContent()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello 🌍 世界 🎉");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("hello 🌍 世界 🎉", text);
    }

    [Fact]
    public async Task ReadToEnd_ExceedsMaxBytes_Throws()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite(new string('a', 100));
        ctx.Complete();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ctx.ReadToEndAsync(maxBytes: 50));
    }

    [Fact]
    public async Task ReadToEnd_MultipleChunks_ConcatenatesAll()
    {
        await using var ctx = new PasteContext();
        ctx.TryWrite("hello ");
        ctx.TryWrite("world");
        ctx.TryWrite("!");
        ctx.Complete();

        var text = await ctx.ReadToEndAsync();
        Assert.Equal("hello world!", text);
    }

    [Fact]
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

        Assert.Equal(3, chunks.Count);
        Assert.Equal("chunk1", chunks[0]);
        Assert.Equal("chunk2", chunks[1]);
        Assert.Equal("chunk3", chunks[2]);
    }

    [Fact]
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

        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
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

        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
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
        Assert.Equal("hello world", text);
    }

    [Fact]
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
            Assert.Equal("file content", content);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
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
        Assert.True(chunks.Count <= 1);
    }

    [Fact]
    public void Cancel_SignalsCancellationToken()
    {
        var ctx = new PasteContext();
        try
        {
            Assert.False(ctx.CancellationToken.IsCancellationRequested);

            ctx.Cancel();

            Assert.True(ctx.CancellationToken.IsCancellationRequested);
            Assert.True(ctx.IsCancelled);
        }
        finally
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task Completed_SignaledOnComplete()
    {
        await using var ctx = new PasteContext();
        Assert.False(ctx.IsCompleted);

        ctx.Complete();

        await ctx.Completed; // Should not hang
        Assert.True(ctx.IsCompleted);
    }

    [Fact]
    public async Task Completed_SignaledOnCancel()
    {
        await using var ctx = new PasteContext();

        ctx.Cancel();

        await ctx.Completed; // Should not hang
        Assert.True(ctx.IsCancelled);
    }

    [Fact]
    public async Task Dispose_CompletesContext()
    {
        var ctx = new PasteContext();
        await ctx.DisposeAsync();

        Assert.True(ctx.IsCancelled);
    }

    [Fact]
    public async Task WriteAsync_AfterCancel_ReturnsFalse()
    {
        await using var ctx = new PasteContext();
        ctx.Cancel();

        var written = await ctx.WriteAsync("should not write");
        Assert.False(written);
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_ReturnsFalse()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();

        var written = await ctx.WriteAsync("should not write");
        Assert.False(written);
    }

    [Fact]
    public void TryWrite_AfterCancel_ReturnsFalse()
    {
        var ctx = new PasteContext();
        try
        {
            ctx.Cancel();
            Assert.False(ctx.TryWrite("should not write"));
        }
        finally
        {
            ctx.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task Cancel_Idempotent()
    {
        await using var ctx = new PasteContext();
        ctx.Cancel();
        ctx.Cancel(); // Should not throw

        Assert.True(ctx.IsCancelled);
    }

    [Fact]
    public async Task Complete_Idempotent()
    {
        await using var ctx = new PasteContext();
        ctx.Complete();
        ctx.Complete(); // Should not throw

        Assert.True(ctx.IsCompleted);
    }

    [Fact]
    public async Task Invalidate_CalledWhenProvided()
    {
        int invalidateCount = 0;
        await using var ctx = new PasteContext(invalidate: () => invalidateCount++);

        ctx.Invalidate();
        ctx.Invalidate();

        Assert.Equal(2, invalidateCount);
    }

    [Fact]
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
            Assert.Equal("hello", text);
        }
        catch (OperationCanceledException)
        {
            // Also acceptable
        }
    }

    [Fact]
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

        Assert.Equal(2, receivedChunks.Count);
        Assert.Equal("chunk1", receivedChunks[0]);
        Assert.Equal("chunk2", receivedChunks[1]);
    }

    [Fact]
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

        Assert.Equal(2, lines.Count);
        Assert.Equal("hello", lines[0]);
        Assert.Equal("world", lines[1]);
    }
}
