using System.Text;
using Hex1b;
using Xunit;

namespace Hex1b.Tests;

public class AsciinemaFileWorkloadAdapterTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_test_{Guid.NewGuid()}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    private async Task<string> CreateTestCastFile(string filePath)
    {
        // Create a simple asciicast v2 file with header and a few events
        var lines = new[]
        {
            "{\"version\":2,\"width\":80,\"height\":24,\"timestamp\":1234567890}",
            "[0.0,\"o\",\"Hello, World!\\r\\n\"]",
            "[0.5,\"o\",\"Second line\\r\\n\"]",
            "[1.0,\"o\",\"Third line\\r\\n\"]"
        };

        await File.WriteAllLinesAsync(filePath, lines);
        return filePath;
    }

    [Fact]
    public async Task Constructor_WithValidFilePath_CreatesAdapter()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);

        // Act
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AsciinemaFileWorkloadAdapter(null!));
    }

    [Fact]
    public void Constructor_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AsciinemaFileWorkloadAdapter(""));
    }

    [Fact]
    public async Task StartAsync_WithNonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        await using var adapter = new AsciinemaFileWorkloadAdapter("/nonexistent/file.cast");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await adapter.StartAsync());
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act
        await adapter.StartAsync();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.StartAsync());
    }

    [Fact]
    public async Task ReadOutputAsync_ReadsEventsFromFile()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act
        await adapter.StartAsync();

        // Give playback time to start
        await Task.Delay(100);

        var output1 = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        var text1 = Encoding.UTF8.GetString(output1.Span);

        // Assert
        Assert.Contains("Hello, World!", text1);
    }

    [Fact]
    public async Task ReadOutputAsync_RespectsTimingDelays()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act
        await adapter.StartAsync();

        var startTime = DateTime.UtcNow;
        
        // Read first event (at 0.0s)
        await Task.Delay(100);
        var output1 = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(output1.ToArray());

        // Read second event (at 0.5s, should have ~500ms delay)
        var output2 = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(output2.ToArray());

        var elapsed = DateTime.UtcNow - startTime;

        // Should have taken at least 400ms (allowing some margin)
        Assert.True(elapsed.TotalMilliseconds >= 400, 
            $"Expected at least 400ms delay, got {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task SpeedMultiplier_AffectsPlaybackSpeed()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath)
        {
            SpeedMultiplier = 2.0 // 2x speed
        };

        // Act
        await adapter.StartAsync();

        var startTime = DateTime.UtcNow;

        // Read events
        await Task.Delay(50);
        var output1 = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(output1.ToArray());

        var output2 = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(output2.ToArray());

        var elapsed = DateTime.UtcNow - startTime;

        // At 2x speed, 0.5s delay should take ~250ms
        // Allow some margin (should be less than 400ms)
        Assert.True(elapsed.TotalMilliseconds < 400,
            $"Expected less than 400ms at 2x speed, got {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task SpeedMultiplier_SetToZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var filePath = GetTempFile();
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.SpeedMultiplier = 0);
    }

    [Fact]
    public async Task SpeedMultiplier_SetToNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var filePath = GetTempFile();
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.SpeedMultiplier = -1.0);
    }

    [Fact]
    public async Task Width_Height_ReadFromHeader()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act
        await adapter.StartAsync();
        await Task.Delay(100); // Give time to read header

        // Assert
        Assert.Equal(80, adapter.Width);
        Assert.Equal(24, adapter.Height);
    }

    [Fact]
    public async Task Disconnected_FiredWhenPlaybackCompletes()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath)
        {
            SpeedMultiplier = 10.0 // Speed up for faster test
        };

        var disconnectedFired = false;
        adapter.Disconnected += () => disconnectedFired = true;

        // Act
        await adapter.StartAsync();

        // Read all events
        while (true)
        {
            var output = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
            if (output.IsEmpty)
                break;
        }

        // Wait a bit for Disconnected event
        await Task.Delay(200);

        // Assert
        Assert.True(disconnectedFired);
    }

    [Fact]
    public async Task WriteInputAsync_DoesNotThrow()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act & Assert - should not throw
        await adapter.WriteInputAsync(Encoding.UTF8.GetBytes("test"));
    }

    [Fact]
    public async Task ResizeAsync_DoesNotThrow()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        await using var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        // Act & Assert - should not throw
        await adapter.ResizeAsync(100, 40);
    }

    [Fact]
    public async Task DisposeAsync_StopsPlayback()
    {
        // Arrange
        var filePath = GetTempFile();
        await CreateTestCastFile(filePath);
        var adapter = new AsciinemaFileWorkloadAdapter(filePath);

        await adapter.StartAsync();
        await Task.Delay(100);

        // Act
        await adapter.DisposeAsync();

        // Assert - subsequent reads should return empty
        var output = await adapter.ReadOutputAsync(TestContext.Current.CancellationToken);
        Assert.True(output.IsEmpty);
    }
}
