using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for the ScrollbackBuffer circular buffer data structure.
/// </summary>
public class ScrollbackBufferTests
{
    private static ScrollbackBuffer CreateBuffer(int capacity = 5)
        => new(capacity);

    private static TerminalCell[] MakeRow(string text, int width = 10)
    {
        var cells = new TerminalCell[width];
        for (int i = 0; i < width; i++)
        {
            cells[i] = i < text.Length
                ? new TerminalCell(text[i].ToString(), null, null)
                : TerminalCell.Empty;
        }
        return cells;
    }

    [Fact]
    public void Constructor_ThrowsForZeroCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScrollbackBuffer(0));
    }

    [Fact]
    public void Constructor_ThrowsForNegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScrollbackBuffer(-1));
    }

    [Fact]
    public void Push_WithinCapacity_IncrementsCount()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        Assert.Equal(1, buffer.Count);

        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Push_AtCapacity_CountDoesNotExceedCapacity()
    {
        var buffer = CreateBuffer(3);

        for (int i = 0; i < 5; i++)
            buffer.Push(MakeRow($"{i}"), 10, DateTimeOffset.UtcNow);

        Assert.Equal(3, buffer.Count);
    }

    [Fact]
    public void Push_AtCapacity_ReturnsEvictedRow()
    {
        var buffer = CreateBuffer(2);

        var evicted1 = buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        var evicted2 = buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        var evicted3 = buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        Assert.Null(evicted1);
        Assert.Null(evicted2);
        Assert.NotNull(evicted3);
        Assert.Equal("A", evicted3.Value.Cells[0].Character);
    }

    [Fact]
    public void GetLines_ReturnsOldestToNewest()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(3);

        Assert.Equal(3, lines.Length);
        Assert.Equal("A", lines[0].Cells[0].Character);
        Assert.Equal("B", lines[1].Cells[0].Character);
        Assert.Equal("C", lines[2].Cells[0].Character);
    }

    [Fact]
    public void GetLines_AfterEviction_ReturnsCorrectOrder()
    {
        var buffer = CreateBuffer(3);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("D"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("E"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(3);

        Assert.Equal(3, lines.Length);
        Assert.Equal("C", lines[0].Cells[0].Character);
        Assert.Equal("D", lines[1].Cells[0].Character);
        Assert.Equal("E", lines[2].Cells[0].Character);
    }

    [Fact]
    public void GetLines_RequestMoreThanAvailable_ReturnsAvailable()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(10);

        Assert.Equal(2, lines.Length);
        Assert.Equal("A", lines[0].Cells[0].Character);
        Assert.Equal("B", lines[1].Cells[0].Character);
    }

    [Fact]
    public void GetLines_RequestFewerThanAvailable_ReturnsMostRecent()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(2);

        Assert.Equal(2, lines.Length);
        Assert.Equal("B", lines[0].Cells[0].Character);
        Assert.Equal("C", lines[1].Cells[0].Character);
    }

    [Fact]
    public void GetLines_ZeroCount_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);
        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(0);
        Assert.Empty(lines);
    }

    [Fact]
    public void GetLines_NegativeCount_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);
        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(-1);
        Assert.Empty(lines);
    }

    [Fact]
    public void GetLines_EmptyBuffer_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);

        var lines = buffer.GetLines(5);
        Assert.Empty(lines);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.GetLines(10));
    }

    [Fact]
    public void Clear_OnEmptyBuffer_DoesNotThrow()
    {
        var buffer = CreateBuffer(5);
        buffer.Clear(); // Should not throw
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Push_PreservesOriginalWidth()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A", 80), 80, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B", 120), 120, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(2);
        Assert.Equal(80, lines[0].OriginalWidth);
        Assert.Equal(120, lines[1].OriginalWidth);
    }

    [Fact]
    public void Push_PreservesTimestamp()
    {
        var buffer = CreateBuffer(5);
        var ts = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        buffer.Push(MakeRow("A"), 10, ts);

        var lines = buffer.GetLines(1);
        Assert.Equal(ts, lines[0].Timestamp);
    }

    [Fact]
    public void Capacity_ReturnsConfiguredValue()
    {
        var buffer = CreateBuffer(42);
        Assert.Equal(42, buffer.Capacity);
    }
}
