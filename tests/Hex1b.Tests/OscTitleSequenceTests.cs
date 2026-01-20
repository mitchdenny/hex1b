using Hex1b.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC 0/1/2 (title/icon) and OSC 22/23 (title stack push/pop) sequences.
/// </summary>
public class OscTitleSequenceTests
{
    #region OSC 0 - Set Both Title and Icon

    [Fact]
    public async Task Osc0_SetsBothWindowTitleAndIconName()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Send OSC 0 sequence (BEL terminator)
        workload.Write("\x1b]0;My Terminal Title\x07");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("My Terminal Title", terminal.WindowTitle);
        Assert.Equal("My Terminal Title", terminal.IconName);
    }

    [Fact]
    public async Task Osc0_WithStTerminator_SetsBothTitleAndIcon()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Send OSC 0 sequence with ST (ESC \) terminator
        workload.Write("\x1b]0;Title With ST\x1b\\");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Title With ST", terminal.WindowTitle);
        Assert.Equal("Title With ST", terminal.IconName);
    }

    [Fact]
    public async Task Osc0_FiresBothEvents()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        string? capturedTitle = null;
        string? capturedIcon = null;
        terminal.WindowTitleChanged += t => capturedTitle = t;
        terminal.IconNameChanged += i => capturedIcon = i;

        workload.Write("\x1b]0;Event Test\x07");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Event Test", capturedTitle);
        Assert.Equal("Event Test", capturedIcon);
    }

    [Fact]
    public async Task Osc0_EmptyTitle_SetsEmptyString()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // First set a title
        workload.Write("\x1b]0;Initial Title\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Initial Title", terminal.WindowTitle);

        // Now clear it
        workload.Write("\x1b]0;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("", terminal.WindowTitle);
        Assert.Equal("", terminal.IconName);
    }

    #endregion

    #region OSC 1 - Set Icon Name Only

    [Fact]
    public async Task Osc1_SetsIconNameOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // First set a different title
        workload.Write("\x1b]0;Initial\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Now set icon only
        workload.Write("\x1b]1;Icon Only\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Initial", terminal.WindowTitle); // Unchanged
        Assert.Equal("Icon Only", terminal.IconName);
    }

    [Fact]
    public async Task Osc1_FiresOnlyIconEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        int titleEventCount = 0;
        int iconEventCount = 0;
        terminal.WindowTitleChanged += _ => titleEventCount++;
        terminal.IconNameChanged += _ => iconEventCount++;

        workload.Write("\x1b]1;Icon Event\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal(0, titleEventCount); // Title should not fire
        Assert.Equal(1, iconEventCount);
    }

    #endregion

    #region OSC 2 - Set Window Title Only

    [Fact]
    public async Task Osc2_SetsWindowTitleOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // First set a different icon
        workload.Write("\x1b]0;Initial\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Now set title only
        workload.Write("\x1b]2;Title Only\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Title Only", terminal.WindowTitle);
        Assert.Equal("Initial", terminal.IconName); // Unchanged
    }

    [Fact]
    public async Task Osc2_FiresOnlyTitleEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        int titleEventCount = 0;
        int iconEventCount = 0;
        terminal.WindowTitleChanged += _ => titleEventCount++;
        terminal.IconNameChanged += _ => iconEventCount++;

        workload.Write("\x1b]2;Title Event\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal(1, titleEventCount);
        Assert.Equal(0, iconEventCount); // Icon should not fire
    }

    #endregion

    #region OSC 22/23 - Title Stack

    [Fact]
    public async Task Osc22_PushesCurrentTitleOntoStack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Set initial title
        workload.Write("\x1b]0;Original\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Original", terminal.WindowTitle);

        // Push current title onto stack
        workload.Write("\x1b]22;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Title should still be the same after push
        Assert.Equal("Original", terminal.WindowTitle);
    }

    [Fact]
    public async Task Osc23_PopsAndRestoresTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Set initial title
        workload.Write("\x1b]0;Original\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Push
        workload.Write("\x1b]22;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Change title
        workload.Write("\x1b]0;New Title\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("New Title", terminal.WindowTitle);

        // Pop - should restore to "Original"
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Original", terminal.WindowTitle);
        Assert.Equal("Original", terminal.IconName);
    }

    [Fact]
    public async Task Osc22_23_StackIsIndependentOfDirectTitleChanges()
    {
        // This tests the key behavior: OSC 0/1/2 do NOT affect the stack
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Set initial: Title="bash", Icon="bash"
        workload.Write("\x1b]0;bash\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Push: Stack=["bash"], Current="bash"
        workload.Write("\x1b]22;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Set new title with OSC 0: Stack=["bash"], Current="vim"
        workload.Write("\x1b]0;vim\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("vim", terminal.WindowTitle);

        // Pop: Stack=[], Current="bash" (restored from stack, NOT "vim")
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("bash", terminal.WindowTitle);
    }

    [Fact]
    public async Task Osc22_23_MultipleStackLevels()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Level 0: "shell"
        workload.Write("\x1b]0;shell\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Push level 1
        workload.Write("\x1b]22;\x07");
        workload.Write("\x1b]0;vim\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("vim", terminal.WindowTitle);

        // Push level 2
        workload.Write("\x1b]22;\x07");
        workload.Write("\x1b]0;:help\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal(":help", terminal.WindowTitle);

        // Pop level 2 -> vim
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("vim", terminal.WindowTitle);

        // Pop level 1 -> shell
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("shell", terminal.WindowTitle);
    }

    [Fact]
    public async Task Osc23_OnEmptyStack_IsIgnored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Set a title
        workload.Write("\x1b]0;Current\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Pop without any push - should be ignored
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Title should remain unchanged
        Assert.Equal("Current", terminal.WindowTitle);
    }

    [Fact]
    public async Task Osc22_WithPayload_PushesAndSetsNewTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Set initial
        workload.Write("\x1b]0;Original\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Push with new title in payload
        workload.Write("\x1b]22;New After Push\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Current should be the new title
        Assert.Equal("New After Push", terminal.WindowTitle);

        // Pop should restore to original
        workload.Write("\x1b]23;\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("Original", terminal.WindowTitle);
    }

    #endregion

    #region Event Deduplication

    [Fact]
    public async Task TitleEvent_NotFiredWhenTitleUnchanged()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        int titleEventCount = 0;
        terminal.WindowTitleChanged += _ => titleEventCount++;

        // Set title first time
        workload.Write("\x1b]2;Same Title\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal(1, titleEventCount);

        // Set same title again
        workload.Write("\x1b]2;Same Title\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Should not fire again
        Assert.Equal(1, titleEventCount);
    }

    #endregion

    #region Special Characters

    [Fact]
    public async Task Title_WithSpecialCharacters_IsPreserved()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Title with special characters
        workload.Write("\x1b]0;~/Code/project - vim [+]\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("~/Code/project - vim [+]", terminal.WindowTitle);
    }

    [Fact]
    public async Task Title_WithUnicode_IsPreserved()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Title with Unicode characters
        workload.Write("\x1b]0;📁 Project - 日本語\x07");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("📁 Project - 日本語", terminal.WindowTitle);
    }

    #endregion

    #region Initial State

    [Fact]
    public void Terminal_InitialTitleIsEmpty()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        Assert.Equal("", terminal.WindowTitle);
        Assert.Equal("", terminal.IconName);
    }

    #endregion
}
