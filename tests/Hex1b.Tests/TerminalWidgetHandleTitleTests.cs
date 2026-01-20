using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC title sequences in TerminalWidgetHandle.
/// </summary>
public class TerminalWidgetHandleTitleTests
{
    // Helper to create AppliedToken for OSC sequences (no cell impacts)
    private static AppliedToken OscApplied(string command, string payload = "")
        => AppliedToken.WithNoCellImpacts(new OscToken(command, "", payload), 0, 0, 0, 0);

    [Fact]
    public async Task Osc0_SetsBothTitleAndIcon()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Test Title")]);
        
        Assert.Equal("Test Title", handle.WindowTitle);
        Assert.Equal("Test Title", handle.IconName);
    }
    
    [Fact]
    public async Task Osc1_SetsIconOnly()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // First set both
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Initial")]);
        
        // Then set icon only
        await handle.WriteOutputWithImpactsAsync([OscApplied("1", "Icon")]);
        
        Assert.Equal("Initial", handle.WindowTitle); // Unchanged
        Assert.Equal("Icon", handle.IconName);
    }
    
    [Fact]
    public async Task Osc2_SetsTitleOnly()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // First set both
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Initial")]);
        
        // Then set title only
        await handle.WriteOutputWithImpactsAsync([OscApplied("2", "Title")]);
        
        Assert.Equal("Title", handle.WindowTitle);
        Assert.Equal("Initial", handle.IconName); // Unchanged
    }
    
    [Fact]
    public async Task Osc22_23_PushPop()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // Set initial
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Original")]);
        
        // Push
        await handle.WriteOutputWithImpactsAsync([OscApplied("22", "")]);
        
        // Change
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Changed")]);
        
        Assert.Equal("Changed", handle.WindowTitle);
        
        // Pop
        await handle.WriteOutputWithImpactsAsync([OscApplied("23", "")]);
        
        Assert.Equal("Original", handle.WindowTitle);
    }
    
    [Fact]
    public async Task WindowTitleChanged_EventFires()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        string? captured = null;
        handle.WindowTitleChanged += t => captured = t;
        
        await handle.WriteOutputWithImpactsAsync([OscApplied("2", "Event Test")]);
        
        Assert.Equal("Event Test", captured);
    }
    
    [Fact]
    public async Task IconNameChanged_EventFires()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        string? captured = null;
        handle.IconNameChanged += i => captured = i;
        
        await handle.WriteOutputWithImpactsAsync([OscApplied("1", "Icon Event")]);
        
        Assert.Equal("Icon Event", captured);
    }
    
    [Fact]
    public async Task TitleChange_DoesNotFireWhenUnchanged()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        int eventCount = 0;
        handle.WindowTitleChanged += _ => eventCount++;
        
        // Set first time
        await handle.WriteOutputWithImpactsAsync([OscApplied("2", "Same")]);
        
        Assert.Equal(1, eventCount);
        
        // Set same again
        await handle.WriteOutputWithImpactsAsync([OscApplied("2", "Same")]);
        
        Assert.Equal(1, eventCount); // Should not increment
    }
    
    [Fact]
    public void InitialState_TitleAndIconAreEmpty()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        Assert.Equal("", handle.WindowTitle);
        Assert.Equal("", handle.IconName);
    }
    
    [Fact]
    public async Task Osc22_WithPayload_PushesAndSets()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // Set initial
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Original")]);
        
        // Push with new title
        await handle.WriteOutputWithImpactsAsync([OscApplied("22", "New Title")]);
        
        Assert.Equal("New Title", handle.WindowTitle);
        
        // Pop should restore
        await handle.WriteOutputWithImpactsAsync([OscApplied("23", "")]);
        
        Assert.Equal("Original", handle.WindowTitle);
    }
    
    [Fact]
    public async Task Osc23_EmptyStack_IsIgnored()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // Set a title without pushing
        await handle.WriteOutputWithImpactsAsync([OscApplied("0", "Current")]);
        
        // Pop on empty stack
        await handle.WriteOutputWithImpactsAsync([OscApplied("23", "")]);
        
        // Should remain unchanged
        Assert.Equal("Current", handle.WindowTitle);
    }
}
