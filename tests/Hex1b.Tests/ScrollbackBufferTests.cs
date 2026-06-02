using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for the ScrollbackBuffer circular buffer data structure.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void Constructor_ThrowsForZeroCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScrollbackBuffer(0));
    }

    [TestMethod]
    public void Constructor_ThrowsForNegativeCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScrollbackBuffer(-1));
    }

    [TestMethod]
    public void Push_WithinCapacity_IncrementsCount()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        Assert.AreEqual(1, buffer.Count);

        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        Assert.AreEqual(2, buffer.Count);
    }

    [TestMethod]
    public void Push_AtCapacity_CountDoesNotExceedCapacity()
    {
        var buffer = CreateBuffer(3);

        for (int i = 0; i < 5; i++)
            buffer.Push(MakeRow($"{i}"), 10, DateTimeOffset.UtcNow);

        Assert.AreEqual(3, buffer.Count);
    }

    [TestMethod]
    public void Push_AtCapacity_ReturnsEvictedRow()
    {
        var buffer = CreateBuffer(2);

        var evicted1 = buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        var evicted2 = buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        var evicted3 = buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        Assert.IsNull(evicted1);
        Assert.IsNull(evicted2);
        Assert.IsNotNull(evicted3);
        Assert.AreEqual("A", evicted3.Value.Cells[0].Character);
    }

    [TestMethod]
    public void GetLines_ReturnsOldestToNewest()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(3);

        Assert.AreEqual(3, lines.Length);
        Assert.AreEqual("A", lines[0].Cells[0].Character);
        Assert.AreEqual("B", lines[1].Cells[0].Character);
        Assert.AreEqual("C", lines[2].Cells[0].Character);
    }

    [TestMethod]
    public void GetLines_AfterEviction_ReturnsCorrectOrder()
    {
        var buffer = CreateBuffer(3);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("D"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("E"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(3);

        Assert.AreEqual(3, lines.Length);
        Assert.AreEqual("C", lines[0].Cells[0].Character);
        Assert.AreEqual("D", lines[1].Cells[0].Character);
        Assert.AreEqual("E", lines[2].Cells[0].Character);
    }

    [TestMethod]
    public void GetLines_RequestMoreThanAvailable_ReturnsAvailable()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(10);

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("A", lines[0].Cells[0].Character);
        Assert.AreEqual("B", lines[1].Cells[0].Character);
    }

    [TestMethod]
    public void GetLines_RequestFewerThanAvailable_ReturnsMostRecent()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("C"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(2);

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("B", lines[0].Cells[0].Character);
        Assert.AreEqual("C", lines[1].Cells[0].Character);
    }

    [TestMethod]
    public void GetLines_ZeroCount_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);
        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(0);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void GetLines_NegativeCount_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);
        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(-1);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void GetLines_EmptyBuffer_ReturnsEmpty()
    {
        var buffer = CreateBuffer(5);

        var lines = buffer.GetLines(5);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void Clear_EmptiesBuffer()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A"), 10, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B"), 10, DateTimeOffset.UtcNow);

        buffer.Clear();

        Assert.AreEqual(0, buffer.Count);
        Assert.IsEmpty(buffer.GetLines(10));
    }

    [TestMethod]
    public void Clear_OnEmptyBuffer_DoesNotThrow()
    {
        var buffer = CreateBuffer(5);
        buffer.Clear(); // Should not throw
        Assert.AreEqual(0, buffer.Count);
    }

    [TestMethod]
    public void Push_PreservesOriginalWidth()
    {
        var buffer = CreateBuffer(5);

        buffer.Push(MakeRow("A", 80), 80, DateTimeOffset.UtcNow);
        buffer.Push(MakeRow("B", 120), 120, DateTimeOffset.UtcNow);

        var lines = buffer.GetLines(2);
        Assert.AreEqual(80, lines[0].OriginalWidth);
        Assert.AreEqual(120, lines[1].OriginalWidth);
    }

    [TestMethod]
    public void Push_PreservesTimestamp()
    {
        var buffer = CreateBuffer(5);
        var ts = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        buffer.Push(MakeRow("A"), 10, ts);

        var lines = buffer.GetLines(1);
        Assert.AreEqual(ts, lines[0].Timestamp);
    }

    [TestMethod]
    public void Capacity_ReturnsConfiguredValue()
    {
        var buffer = CreateBuffer(42);
        Assert.AreEqual(42, buffer.Capacity);
    }
}
