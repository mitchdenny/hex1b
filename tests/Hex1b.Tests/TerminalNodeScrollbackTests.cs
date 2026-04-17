using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for terminal scrollback support in TerminalNode and TerminalWidgetHandle.
/// </summary>
public class TerminalNodeScrollbackTests
{
    // Helper to create AppliedToken for PrivateMode sequences
    private static AppliedToken PrivateModeApplied(int mode, bool enable)
        => AppliedToken.WithNoCellImpacts(new PrivateModeToken(mode, enable), 0, 0, 0, 0);

    #region TerminalWidgetHandle - Alternate Screen Tracking

    [Fact]
    public void Handle_InAlternateScreen_DefaultsFalse()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        Assert.False(handle.InAlternateScreen);
    }

    [Fact]
    public async Task Handle_PrivateMode1049Enable_SetsInAlternateScreen()
    {
        var handle = new TerminalWidgetHandle(80, 24);

        await handle.WriteOutputWithImpactsAsync([PrivateModeApplied(1049, true)]);

        Assert.True(handle.InAlternateScreen);
    }

    [Fact]
    public async Task Handle_PrivateMode1049Disable_ClearsInAlternateScreen()
    {
        var handle = new TerminalWidgetHandle(80, 24);

        await handle.WriteOutputWithImpactsAsync([PrivateModeApplied(1049, true)]);
        await handle.WriteOutputWithImpactsAsync([PrivateModeApplied(1049, false)]);

        Assert.False(handle.InAlternateScreen);
    }

    #endregion

    #region TerminalWidgetHandle - Scrollback Access

    [Fact]
    public void Handle_ScrollbackCount_ReturnsZeroWithoutTerminal()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        Assert.Equal(0, handle.ScrollbackCount);
    }

    [Fact]
    public void Handle_GetScrollbackSnapshot_ReturnsEmptyWithoutTerminal()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        var rows = handle.GetScrollbackSnapshot(100);
        Assert.Empty(rows);
    }

    [Fact]
    public void Handle_ScrollbackCount_ReturnsCountWhenTerminalHasScrollback()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        var terminal = CreateTerminalWithScrollback(80, 24, scrollbackCapacity: 100);
        SetTerminalOnHandle(handle, terminal);
        PopulateScrollback(terminal, 50);

        Assert.True(handle.ScrollbackCount > 0);
    }

    [Fact]
    public void Handle_GetScrollbackSnapshot_ReturnsRowsWhenTerminalHasScrollback()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        var terminal = CreateTerminalWithScrollback(80, 24, scrollbackCapacity: 100);
        SetTerminalOnHandle(handle, terminal);
        PopulateScrollback(terminal, 50);

        var rows = handle.GetScrollbackSnapshot(100);
        Assert.NotEmpty(rows);
    }

    #endregion

    #region TerminalNode - Scrollback Offset

    [Fact]
    public void ScrollbackOffset_DefaultsToZero()
    {
        var node = new TerminalNode();
        Assert.Equal(0, node.ScrollbackOffset);
        Assert.False(node.IsInScrollbackMode);
    }

    #endregion

    #region TerminalNode - Input Interception

    [Fact]
    public void HandleInput_WhenInScrollbackMode_DoesNotForwardKeyboard()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        // Manually set scrollback offset to simulate being in scrollback mode
        SetScrollbackOffset(node, 5);

        var keyEvent = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None);
        var result = node.HandleInput(keyEvent);

        // Should return NotHandled (not forwarded to terminal)
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleInput_WhenNotInScrollbackMode_ForwardsKeyboard()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        var keyEvent = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None);
        var result = node.HandleInput(keyEvent);

        // Should return Handled (forwarded to terminal)
        Assert.Equal(InputResult.Handled, result);
    }

    [Fact]
    public void HandleInput_MouseEvent_WhenInScrollbackMode_StillForwarded()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        SetScrollbackOffset(node, 5);

        // Non-keyboard events should still be forwarded (scrollback only intercepts keyboard)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleInput(mouseEvent);

        Assert.Equal(InputResult.Handled, result);
    }

    #endregion

    #region TerminalNode - Mouse Scroll Routing

    [Fact]
    public async Task HandleMouseClick_ScrollWheel_WhenMouseTrackingEnabled_ForwardsToChild()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        // Enable mouse tracking via escape sequence
        await handle.WriteOutputWithImpactsAsync([PrivateModeApplied(1000, true)]);

        var scrollEvent = new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 5, scrollEvent);

        // When child has mouse tracking, scroll events are forwarded
        Assert.Equal(InputResult.Handled, result);
    }

    [Fact]
    public void HandleMouseClick_ScrollWheel_WhenNoMouseTracking_ReturnsNotHandled()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        var scrollEvent = new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 5, scrollEvent);

        // Scroll events should return NotHandled so input binding system handles them
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleMouseClick_NonScroll_WhenNoMouseTracking_ForwardsToChild()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        var clickEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 5, clickEvent);

        // Non-scroll mouse events are forwarded
        Assert.Equal(InputResult.Handled, result);
    }

    [Fact]
    public void HandleMouseClick_ScrollDown_WhenNoMouseTracking_ReturnsNotHandled()
    {
        var handle = CreateRunningHandle();
        var node = CreateBoundNode(handle);

        var scrollEvent = new Hex1bMouseEvent(MouseButton.ScrollDown, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 5, scrollEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion

    #region TerminalNode - ConfigureDefaultBindings

    [Fact]
    public void ConfigureDefaultBindings_RegistersAllScrollbackActions()
    {
        var node = new TerminalNode();
        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        var actionIds = bindings.GetAllActionIds();
        Assert.Contains(TerminalWidget.ScrollUpLine, actionIds);
        Assert.Contains(TerminalWidget.ScrollDownLine, actionIds);
        Assert.Contains(TerminalWidget.ScrollUpPage, actionIds);
        Assert.Contains(TerminalWidget.ScrollDownPage, actionIds);
        Assert.Contains(TerminalWidget.ScrollToTop, actionIds);
        Assert.Contains(TerminalWidget.ScrollToBottom, actionIds);
    }

    [Fact]
    public void ConfigureDefaultBindings_RegistersMouseScrollBindings()
    {
        var node = new TerminalNode();
        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        var mouseBindings = bindings.MouseBindings;
        Assert.Contains(mouseBindings, b => b.Button == MouseButton.ScrollUp);
        Assert.Contains(mouseBindings, b => b.Button == MouseButton.ScrollDown);
    }

    [Fact]
    public void ConfigureDefaultBindings_HasKeyboardBindingsForScrollback()
    {
        var node = new TerminalNode();
        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        // Should have 6 keyboard bindings (ScrollUpLine, ScrollDownLine, ScrollUpPage, ScrollDownPage, ScrollToTop, ScrollToBottom)
        Assert.True(bindings.Bindings.Count >= 6, $"Expected at least 6 key bindings, got {bindings.Bindings.Count}");
    }

    #endregion

    #region TerminalWidget - ActionId Names

    [Fact]
    public void ActionIds_FollowNamingConvention()
    {
        Assert.Equal("TerminalWidget.ScrollUpLine", TerminalWidget.ScrollUpLine.Value);
        Assert.Equal("TerminalWidget.ScrollDownLine", TerminalWidget.ScrollDownLine.Value);
        Assert.Equal("TerminalWidget.ScrollUpPage", TerminalWidget.ScrollUpPage.Value);
        Assert.Equal("TerminalWidget.ScrollDownPage", TerminalWidget.ScrollDownPage.Value);
        Assert.Equal("TerminalWidget.ScrollToTop", TerminalWidget.ScrollToTop.Value);
        Assert.Equal("TerminalWidget.ScrollToBottom", TerminalWidget.ScrollToBottom.Value);
    }

    #endregion

    #region Hex1bTerminal - Scrollback Access

    [Fact]
    public void Terminal_ScrollbackCount_ReturnsZeroWhenNotEnabled()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        Assert.Equal(0, terminal.ScrollbackCount);
    }

    [Fact]
    public void Terminal_ScrollbackCount_ReturnsCountWhenEnabled()
    {
        var terminal = CreateTerminalWithScrollback(80, 24, scrollbackCapacity: 100);
        PopulateScrollback(terminal, 50);

        Assert.True(terminal.ScrollbackCount > 0);
    }

    [Fact]
    public void Terminal_GetScrollbackRows_ReturnsEmptyWhenNotEnabled()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var rows = terminal.GetScrollbackRows(100);
        Assert.Empty(rows);
    }

    [Fact]
    public void Terminal_GetScrollbackRows_ReturnsRowsWhenEnabled()
    {
        var terminal = CreateTerminalWithScrollback(80, 24, scrollbackCapacity: 100);
        PopulateScrollback(terminal, 50);

        var rows = terminal.GetScrollbackRows(100);
        Assert.NotEmpty(rows);
    }

    [Fact]
    public void Terminal_GetScrollbackRows_RespectsCountLimit()
    {
        var terminal = CreateTerminalWithScrollback(80, 24, scrollbackCapacity: 100);
        PopulateScrollback(terminal, 50);

        var rows = terminal.GetScrollbackRows(5);
        Assert.True(rows.Length <= 5);
    }

    #endregion

    #region Helpers

    private static TerminalWidgetHandle CreateRunningHandle()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        var stateField = typeof(TerminalWidgetHandle).GetField("_state",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        stateField!.SetValue(handle, TerminalState.Running);
        return handle;
    }

    private static TerminalNode CreateBoundNode(TerminalWidgetHandle handle)
    {
        var node = new TerminalNode { Handle = handle };
        node.SetInvalidateCallback(() => { });
        node.Bind();
        node.Arrange(new Rect(0, 0, 80, 24));
        return node;
    }

    private static void SetScrollbackOffset(TerminalNode node, int offset)
    {
        var field = typeof(TerminalNode).GetField("_scrollbackOffset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(node, offset);
    }

    private static void SetTerminalOnHandle(TerminalWidgetHandle handle, Hex1bTerminal terminal)
    {
        var terminalField = typeof(TerminalWidgetHandle).GetField("_terminal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        terminalField!.SetValue(handle, terminal);
    }

    private static Hex1bTerminal CreateTerminalWithScrollback(int width, int height, int scrollbackCapacity)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .WithScrollback(scrollbackCapacity)
            .Build();
    }

    private static void PopulateScrollback(Hex1bTerminal terminal, int lineCount)
    {
        // Write enough lines to push content into scrollback
        var tokens = new List<AnsiToken>();
        for (int i = 0; i < lineCount; i++)
        {
            tokens.Add(new TextToken($"Line {i}"));
            tokens.Add(ControlCharacterToken.LineFeed);
        }
        terminal.ApplyTokens(tokens);
    }

    #endregion
}
