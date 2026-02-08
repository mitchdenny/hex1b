using Hex1b.Logging;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tests;

public class CircularBufferTests
{
    [Fact]
    public void Add_SingleItem_CountIsOne()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(42);
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Add_ExceedsCapacity_CountClampsToCapacity()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(3, buffer.Count);
    }

    [Fact]
    public void Add_ExceedsCapacity_OldestItemsDropped()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        var items = buffer.GetItems(0, 3);
        Assert.Equal([2, 3, 4], items);
    }

    [Fact]
    public void GetItems_ReturnsCorrectRange()
    {
        var buffer = new CircularBuffer<int>(10);
        for (int i = 0; i < 5; i++)
            buffer.Add(i);

        var items = buffer.GetItems(1, 3);
        Assert.Equal([1, 2, 3], items);
    }

    [Fact]
    public void GetItems_StartBeyondCount_ReturnsEmpty()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);

        var items = buffer.GetItems(5, 3);
        Assert.Empty(items);
    }

    [Fact]
    public void GetItems_CountExceedsAvailable_ClampsToAvailable()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);
        buffer.Add(2);

        var items = buffer.GetItems(0, 100);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Clear_ResetsCountToZero()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Clear();

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void CollectionChanged_FiredOnAdd()
    {
        var buffer = new CircularBuffer<int>(10);
        var fired = false;
        buffer.CollectionChanged += (_, _) => fired = true;

        buffer.Add(1);

        Assert.True(fired);
    }

    [Fact]
    public void CollectionChanged_FiredOnClear()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);

        var fired = false;
        buffer.CollectionChanged += (_, _) => fired = true;
        buffer.Clear();

        Assert.True(fired);
    }

    [Fact]
    public void Add_ThreadSafe_NoExceptions()
    {
        var buffer = new CircularBuffer<int>(100);
        var tasks = new Task[10];

        for (int t = 0; t < 10; t++)
        {
            var start = t * 100;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    buffer.Add(start + i);
            });
        }

        Task.WaitAll(tasks);

        // Buffer should have exactly capacity items (100)
        Assert.Equal(100, buffer.Count);
    }
}

public class Hex1bLogStoreTests
{
    [Fact]
    public void CreateLogger_ReturnsLogger()
    {
        var store = new Hex1bLogStore();
        var logger = store.CreateLogger("TestCategory");

        Assert.NotNull(logger);
    }

    [Fact]
    public void Logger_LogMessage_AppearsInBuffer()
    {
        var store = new Hex1bLogStore();
        var logger = store.CreateLogger("TestCategory");

        logger.LogInformation("Hello, World!");

        Assert.Equal(1, store.Buffer.Count);
        var items = store.Buffer.GetItems(0, 1);
        Assert.Equal("Hello, World!", items[0].Message);
        Assert.Equal("TestCategory", items[0].Category);
        Assert.Equal(LogLevel.Information, items[0].Level);
    }

    [Fact]
    public void Logger_MultipleCategories_AllAppearInBuffer()
    {
        var store = new Hex1bLogStore();
        var logger1 = store.CreateLogger("Category.A");
        var logger2 = store.CreateLogger("Category.B");

        logger1.LogInformation("From A");
        logger2.LogWarning("From B");

        Assert.Equal(2, store.Buffer.Count);
        var items = store.Buffer.GetItems(0, 2);
        Assert.Equal("Category.A", items[0].Category);
        Assert.Equal("Category.B", items[1].Category);
    }

    [Fact]
    public void DataSource_ReflectsBufferContents()
    {
        var store = new Hex1bLogStore();
        var logger = store.CreateLogger("Test");

        logger.LogInformation("Entry 1");
        logger.LogError("Entry 2");

        var count = store.DataSource.GetItemCountAsync().GetAwaiter().GetResult();
        Assert.Equal(2, count);

        var items = store.DataSource.GetItemsAsync(0, 2).GetAwaiter().GetResult();
        Assert.Equal("Entry 1", items[0].Message);
        Assert.Equal("Entry 2", items[1].Message);
    }

    [Fact]
    public void Logger_IsEnabled_ReturnsTrueForAllLevels()
    {
        var store = new Hex1bLogStore();
        var logger = store.CreateLogger("Test");

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }
}

public class Hex1bLogTableDataSourceTests
{
    [Fact]
    public void GetItemCountAsync_ReturnsBufferCount()
    {
        var buffer = new CircularBuffer<Hex1bLogEntry>(100);
        var dataSource = new Hex1bLogTableDataSource(buffer);

        buffer.Add(new Hex1bLogEntry(DateTime.UtcNow, LogLevel.Information, "Test", "msg", default, null));

        var count = dataSource.GetItemCountAsync().GetAwaiter().GetResult();
        Assert.Equal(1, count);
    }

    [Fact]
    public void CollectionChanged_ForwardedFromBuffer()
    {
        var buffer = new CircularBuffer<Hex1bLogEntry>(100);
        var dataSource = new Hex1bLogTableDataSource(buffer);
        var fired = false;
        dataSource.CollectionChanged += (_, _) => fired = true;

        buffer.Add(new Hex1bLogEntry(DateTime.UtcNow, LogLevel.Information, "Test", "msg", default, null));

        Assert.True(fired);
    }

    [Fact]
    public void Dispose_UnsubscribesFromBuffer()
    {
        var buffer = new CircularBuffer<Hex1bLogEntry>(100);
        var dataSource = new Hex1bLogTableDataSource(buffer);
        var fired = false;
        dataSource.CollectionChanged += (_, _) => fired = true;

        dataSource.Dispose();
        buffer.Add(new Hex1bLogEntry(DateTime.UtcNow, LogLevel.Information, "Test", "msg", default, null));

        Assert.False(fired);
    }
}

public class LoggerPanelNodeTests
{
    [Fact]
    public void IsFollowing_DefaultsToTrue()
    {
        var node = new LoggerPanelNode();
        Assert.True(node.IsFollowing);
    }
}
