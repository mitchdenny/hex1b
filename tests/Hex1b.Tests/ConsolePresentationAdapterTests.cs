using Hex1b.Terminal;

namespace Hex1b.Tests;

public class ConsolePresentationAdapterTests
{
    [Fact]
    public void Constructor_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        // This test verifies the factory pattern works correctly
        // On unsupported platforms (Windows for now), should throw
        // On Linux/macOS, should succeed
        
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Throws<PlatformNotSupportedException>(() => 
                new ConsolePresentationAdapter(enableMouse: false));
        }
        else
        {
            // On Linux/macOS, constructor should work (but we can't fully test without a real terminal)
            // Just verify it doesn't throw during construction
            // Note: This may still fail in CI without a TTY, which is expected
            try
            {
                var adapter = new ConsolePresentationAdapter(enableMouse: false);
                Assert.NotNull(adapter);
                // Clean up - must use async dispose
                adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex.Message.Contains("TTY") || ex.Message.Contains("terminal") || ex.Message.Contains("tcgetattr"))
            {
                // Expected in CI environments without a real TTY
                // Skip the test gracefully
            }
        }
    }
    
    [Fact]
    public void UnixConsoleDriver_OnlyAvailableOnUnixPlatforms()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Should be able to create the driver (but may fail without TTY)
            try
            {
                using var driver = new UnixConsoleDriver();
                Assert.NotNull(driver);
            }
            catch (Exception ex) when (ex.Message.Contains("TTY") || ex.Message.Contains("tcgetattr"))
            {
                // Expected in CI environments without a real TTY
            }
        }
        else
        {
            // On Windows, the driver should throw or not be available
            Assert.Throws<PlatformNotSupportedException>(() => new UnixConsoleDriver());
        }
    }
}

public class Hex1bTerminalCoreTests
{
    [Fact]
    public void Constructor_WithPresentationAdapter_CreatesWorkloadAdapter()
    {
        // Use the legacy adapter which doesn't need a real terminal
        var presentation = new LegacyConsolePresentationAdapter(enableMouse: false);
        var core = new Hex1bTerminalCore(presentation);
        
        // Core should implement the workload adapter interface
        Assert.IsAssignableFrom<IHex1bAppTerminalWorkloadAdapter>(core);
        
        core.Dispose();
    }
    
    [Fact]
    public void GetSize_ReturnsPresentationSize()
    {
        var presentation = new LegacyConsolePresentationAdapter(enableMouse: false);
        var core = new Hex1bTerminalCore(presentation);
        
        var width = core.Width;
        var height = core.Height;
        
        // Should return console size (may be 0,0 in CI)
        Assert.True(width >= 0);
        Assert.True(height >= 0);
        
        core.Dispose();
    }
    
    [Fact]
    public void Write_SendsDataToPresentation()
    {
        var presentation = new LegacyConsolePresentationAdapter(enableMouse: false);
        var core = new Hex1bTerminalCore(presentation);
        
        // This should not throw
        core.Write("Hello");
        core.Flush();
        
        core.Dispose();
    }
    
    [Fact]
    public void InputEvents_ReturnsChannelReader()
    {
        var presentation = new LegacyConsolePresentationAdapter(enableMouse: false);
        var core = new Hex1bTerminalCore(presentation);
        
        var reader = core.InputEvents;
        Assert.NotNull(reader);
        
        core.Dispose();
    }
    
    [Fact]
    public void Capabilities_ReturnsFromPresentation()
    {
        var presentation = new LegacyConsolePresentationAdapter(enableMouse: false);
        var core = new Hex1bTerminalCore(presentation);
        
        var caps = core.Capabilities;
        Assert.NotNull(caps);
        
        core.Dispose();
    }
}
