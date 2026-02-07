using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowManager functionality using the fluent WindowHandle API.
/// </summary>
public class WindowManagerTests
{
    [Fact]
    public void Open_AddsWindowToManager()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test Window")
            .Size(40, 15);
        
        var entry = manager.Open(handle);

        Assert.NotNull(entry);
        Assert.Equal("Test Window", entry.Title);
        Assert.Equal(40, entry.Width);
        Assert.Equal(15, entry.Height);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Open_WithSameHandle_ReturnsSameEntry()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("One"))
            .Title("Window 1");

        var entry1 = manager.Open(handle);
        var entry2 = manager.Open(handle);

        Assert.Same(entry1, entry2);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Close_RemovesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test");
        
        var entry = manager.Open(handle);

        var result = manager.Close(entry);

        Assert.True(result);
        Assert.Equal(0, manager.Count);
        Assert.False(manager.IsOpen(handle));
    }

    [Fact]
    public void Close_ByHandle_RemovesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test");
        
        manager.Open(handle);

        var result = manager.Close(handle);

        Assert.True(result);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Close_InvokesOnCloseCallback()
    {
        var manager = new WindowManager();
        var callbackInvoked = false;

        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test")
            .OnClose(() => callbackInvoked = true);
        
        manager.Open(handle);
        manager.Close(handle);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void CloseAll_RemovesAllWindows()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        var h3 = manager.Window(_ => new TextBlockWidget("3")).Title("Window 3");
        
        manager.Open(h1);
        manager.Open(h2);
        manager.Open(h3);

        manager.CloseAll();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void CloseAll_InvokesAllCallbacks()
    {
        var manager = new WindowManager();
        var callbackCount = 0;

        var h1 = manager.Window(_ => new TextBlockWidget("1"))
            .Title("Window 1")
            .OnClose(() => callbackCount++);
        var h2 = manager.Window(_ => new TextBlockWidget("2"))
            .Title("Window 2")
            .OnClose(() => callbackCount++);

        manager.Open(h1);
        manager.Open(h2);
        manager.CloseAll();

        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public void BringToFront_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        // Initially entry2 should have higher z-index
        Assert.True(entry2.ZIndex > entry1.ZIndex);

        // Bring entry1 to front
        manager.BringToFront(entry1);

        // Now entry1 should have higher z-index
        Assert.True(entry1.ZIndex > entry2.ZIndex);
    }

    [Fact]
    public void ActiveWindow_ReturnsTopmostWindow()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        Assert.Same(entry2, manager.ActiveWindow);

        manager.BringToFront(entry1);

        Assert.Same(entry1, manager.ActiveWindow);
    }

    [Fact]
    public void ActiveWindow_PrefersModalWindow()
    {
        var manager = new WindowManager();
        var normalHandle = manager.Window(_ => new TextBlockWidget("Normal")).Title("Normal");
        var modalHandle = manager.Window(_ => new TextBlockWidget("Modal")).Title("Modal").Modal();
        
        var normalWindow = manager.Open(normalHandle);
        var modalWindow = manager.Open(modalHandle);

        // Modal should be active even though it was opened second
        Assert.Same(modalWindow, manager.ActiveWindow);

        // Even after bringing normal to front, modal should still be active
        manager.BringToFront(normalWindow);
        Assert.Same(modalWindow, manager.ActiveWindow);
    }

    [Fact]
    public void HasModalWindow_ReturnsTrueWhenModalExists()
    {
        var manager = new WindowManager();
        Assert.False(manager.HasModalWindow);

        var normalHandle = manager.Window(_ => new TextBlockWidget("Normal")).Title("Normal");
        manager.Open(normalHandle);
        Assert.False(manager.HasModalWindow);

        var modalHandle = manager.Window(_ => new TextBlockWidget("Modal")).Title("Modal").Modal();
        manager.Open(modalHandle);
        Assert.True(manager.HasModalWindow);
    }

    [Fact]
    public void Get_ReturnsEntryByHandle()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);

        var found = manager.Get(handle);

        Assert.Same(entry, found);
    }

    [Fact]
    public void Get_ReturnsNullForUnknownHandle()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");

        var found = manager.Get(handle);

        Assert.Null(found);
    }

    [Fact]
    public void IsOpen_ReturnsTrueForOpenWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var otherHandle = manager.Window(_ => new TextBlockWidget("Other")).Title("Other");
        
        manager.Open(handle);

        Assert.True(manager.IsOpen(handle));
        Assert.False(manager.IsOpen(otherHandle));
    }

    [Fact]
    public void All_ReturnsWindowsInZOrder()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        var h3 = manager.Window(_ => new TextBlockWidget("3")).Title("Window 3");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);
        var entry3 = manager.Open(h3);

        var all = manager.All;

        Assert.Equal(3, all.Count);
        // Should be in z-order (entry1 first, entry3 last)
        Assert.Same(entry1, all[0]);
        Assert.Same(entry2, all[1]);
        Assert.Same(entry3, all[2]);
    }

    [Fact]
    public void Changed_EventRaisedOnOpen()
    {
        var manager = new WindowManager();
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        manager.Open(handle);

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnClose()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.Close(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnBringToFront()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.BringToFront(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void WindowEntry_CloseMethod_ClosesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);

        entry.Close();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void WindowEntry_BringToFrontMethod_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        entry1.BringToFront();

        Assert.True(entry1.ZIndex > entry2.ZIndex);
    }

    [Fact]
    public void BringToFront_InvokesOnActivatedCallback()
    {
        var manager = new WindowManager();
        var activated = false;
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2"))
            .Title("Window 2")
            .OnActivated(() => activated = true);

        manager.Open(h1);
        var entry2 = manager.Open(h2);

        // entry2 should already be active since it was just opened
        // Now bring entry1 to front, then entry2 again
        manager.BringToFront(h1);
        activated = false;
        entry2.BringToFront();

        Assert.True(activated);
    }

    [Fact]
    public void BringToFront_InvokesOnDeactivatedCallback()
    {
        var manager = new WindowManager();
        var deactivated = false;
        var h1 = manager.Window(_ => new TextBlockWidget("1"))
            .Title("Window 1")
            .OnDeactivated(() => deactivated = true);
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");

        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        // entry2 is now active, entry1 is not
        // Bring entry1 to front - this should deactivate entry2
        deactivated = false;
        entry1.BringToFront();

        // entry2 doesn't have deactivated callback, but entry1 was previously active
        // Let's check by bringing entry2 to front
        deactivated = false;
        entry2.BringToFront();

        Assert.True(deactivated);
    }

    [Fact]
    public void BringToFront_DoesNotInvokeCallbacks_WhenAlreadyActive()
    {
        var manager = new WindowManager();
        var activatedCount = 0;
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Window")
            .OnActivated(() => activatedCount++);
        
        var entry = manager.Open(handle);

        // Calling BringToFront on already-active window should not trigger callback
        entry.BringToFront();
        entry.BringToFront();
        entry.BringToFront();

        Assert.Equal(0, activatedCount);
    }

    #region Title Bar and Actions Tests

    [Fact]
    public void NoTitleBar_SetsShowTitleBarFalse()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .NoTitleBar();
        
        var entry = manager.Open(handle);

        Assert.False(entry.ShowTitleBar);
    }

    [Fact]
    public void DefaultRightTitleActions_HasCloseButton()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Default Window");
        
        var entry = manager.Open(handle);

        Assert.Single(entry.RightTitleBarActions);
        Assert.Equal("×", entry.RightTitleBarActions[0].Icon);
    }

    [Fact]
    public void RightTitleActions_SetsCustomActions()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Custom Window")
            .RightTitleActions(t => [
                t.Action("?", _ => { }),
                t.Close()
            ]);
        
        var entry = manager.Open(handle);

        Assert.Equal(2, entry.RightTitleBarActions.Count);
        Assert.Equal("?", entry.RightTitleBarActions[0].Icon);
        Assert.Equal("×", entry.RightTitleBarActions[1].Icon);
    }

    [Fact]
    public void EscapeBehavior_SetsCorrectly()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Modal Window")
            .EscapeBehavior(WindowEscapeBehavior.Ignore);
        
        var entry = manager.Open(handle);

        Assert.Equal(WindowEscapeBehavior.Ignore, entry.EscapeBehavior);
    }

    [Fact]
    public void WindowAction_Close_CreatesCloseAction()
    {
        var closeAction = WindowAction.Close();

        Assert.Equal("×", closeAction.Icon);
        Assert.Equal("Close", closeAction.Tooltip);
    }

    [Fact]
    public void WindowAction_Close_WithCustomIcon_CreatesAction()
    {
        var closeAction = WindowAction.Close("X");

        Assert.Equal("X", closeAction.Icon);
    }

    [Fact]
    public void WindowActionContext_Close_ClosesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Test");
        var entry = manager.Open(handle);
        var inputContext = new InputBindingActionContext(new FocusRing());
        var context = new WindowActionContext(entry, inputContext);

        context.Close();

        Assert.False(manager.IsOpen(handle));
    }

    #endregion

    #region Drag/Position Update Tests

    [Fact]
    public void UpdatePosition_ChangesEntryPosition()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Window")
            .Position(10, 5);
        
        var entry = manager.Open(handle);

        manager.UpdatePosition(entry, 20, 15);

        Assert.Equal(20, entry.X);
        Assert.Equal(15, entry.Y);
    }

    [Fact]
    public void UpdatePosition_RaisesChangedEvent()
    {
        var manager = new WindowManager();
        var changedCount = 0;
        manager.Changed += () => changedCount++;
        
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Window");
        var entry = manager.Open(handle);
        changedCount = 0;

        manager.UpdatePosition(entry, 50, 25);

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void UpdatePosition_AllowsNegativeValues()
    {
        // Negative values might be valid during drag before clamping
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Window");
        var entry = manager.Open(handle);

        manager.UpdatePosition(entry, -5, -3);

        Assert.Equal(-5, entry.X);
        Assert.Equal(-3, entry.Y);
    }

    #endregion

    #region Modal Result Tests

    [Fact]
    public void OnResult_InvokesCallbackWithValue()
    {
        var manager = new WindowManager();
        string? receivedValue = null;
        bool? wasCancelled = null;
        
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal()
            .OnResult<string>(result => {
                receivedValue = result.Value;
                wasCancelled = result.IsCancelled;
            });
        
        manager.Open(handle);
        handle.CloseWithResult("test result");

        Assert.Equal("test result", receivedValue);
        Assert.False(wasCancelled);
    }

    [Fact]
    public void OnResult_InvokesCallbackWithCancelledWhenClosedWithoutResult()
    {
        var manager = new WindowManager();
        bool? wasCancelled = null;
        
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal()
            .OnResult<string>(result => {
                wasCancelled = result.IsCancelled;
            });
        
        var entry = manager.Open(handle);
        entry.Close(); // Close without result

        Assert.True(wasCancelled);
    }

    [Fact]
    public void WindowHandle_Cancel_SignalsCancellation()
    {
        var manager = new WindowManager();
        bool? wasCancelled = null;
        string? receivedValue = null;
        
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal()
            .OnResult<string>(result => {
                wasCancelled = result.IsCancelled;
                receivedValue = result.Value;
            });
        
        manager.Open(handle);
        handle.Cancel(); // Explicit cancellation

        Assert.True(wasCancelled);
        Assert.Null(receivedValue);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void CloseWithResult_AlsoClosesWindow()
    {
        var manager = new WindowManager();
        
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal();
        
        var entry = manager.Open(handle);

        entry.CloseWithResult("done");

        Assert.Null(manager.Get(handle));
        Assert.Equal(0, manager.Count);
    }

    #endregion

    #region Fluent WindowHandle API Tests

    [Fact]
    public void Window_CreatesWindowHandle()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"));

        Assert.NotNull(handle);
    }

    [Fact]
    public void WindowHandle_Title_SetsTitle()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("My Window");

        var entry = manager.Open(handle);

        Assert.Equal("My Window", entry.Title);
    }

    [Fact]
    public void WindowHandle_Size_SetsDimensions()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .Size(60, 30);

        var entry = manager.Open(handle);

        Assert.Equal(60, entry.Width);
        Assert.Equal(30, entry.Height);
    }

    [Fact]
    public void WindowHandle_Position_SetsCoordinates()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .Position(10, 20);

        var entry = manager.Open(handle);

        Assert.Equal(10, entry.X);
        Assert.Equal(20, entry.Y);
    }

    [Fact]
    public void WindowHandle_Modal_SetsIsModal()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Modal Window")
            .Modal();

        var entry = manager.Open(handle);

        Assert.True(entry.IsModal);
        Assert.True(manager.HasModalWindow);
    }

    [Fact]
    public void WindowHandle_Resizable_SetsResizableWithConstraints()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Resizable")
            .Resizable(minWidth: 20, minHeight: 10, maxWidth: 100, maxHeight: 50);

        var entry = manager.Open(handle);

        Assert.True(entry.IsResizable);
        Assert.Equal(20, entry.MinWidth);
        Assert.Equal(10, entry.MinHeight);
        Assert.Equal(100, entry.MaxWidth);
        Assert.Equal(50, entry.MaxHeight);
    }

    [Fact]
    public void WindowHandle_NoTitleBar_HidesTitleBar()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .NoTitleBar();

        var entry = manager.Open(handle);

        Assert.False(entry.ShowTitleBar);
    }

    [Fact]
    public void WindowHandle_OnClose_SetsCallback()
    {
        var manager = new WindowManager();
        var callbackInvoked = false;

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .OnClose(() => callbackInvoked = true);

        var entry = manager.Open(handle);
        manager.Close(handle);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void WindowHandle_LeftTitleActions_SetsActions()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .LeftTitleActions(t => [
                t.Action("📌", _ => { }),
                t.Action("📋", _ => { })
            ]);

        var entry = manager.Open(handle);

        Assert.Equal(2, entry.LeftTitleBarActions.Count);
        Assert.Equal("📌", entry.LeftTitleBarActions[0].Icon);
        Assert.Equal("📋", entry.LeftTitleBarActions[1].Icon);
    }

    [Fact]
    public void WindowHandle_RightTitleActions_SetsActions()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .RightTitleActions(t => [
                t.Action("?", _ => { }),
                t.Close()
            ]);

        var entry = manager.Open(handle);

        Assert.Equal(2, entry.RightTitleBarActions.Count);
        Assert.Equal("?", entry.RightTitleBarActions[0].Icon);
        Assert.Equal("×", entry.RightTitleBarActions[1].Icon);
    }

    [Fact]
    public void WindowHandle_DefaultRightTitleActions_HasCloseButton()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        var entry = manager.Open(handle);

        Assert.Single(entry.RightTitleBarActions);
        Assert.Equal("×", entry.RightTitleBarActions[0].Icon);
    }

    [Fact]
    public void Open_WindowHandle_BringsToFrontIfAlreadyOpen()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        var entry1 = manager.Open(handle);
        var entry2 = manager.Open(handle);

        Assert.Same(entry1, entry2);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Close_WindowHandle_RemovesWindow()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        manager.Open(handle);
        var result = manager.Close(handle);

        Assert.True(result);
        Assert.Equal(0, manager.Count);
        Assert.False(manager.IsOpen(handle));
    }

    [Fact]
    public void IsOpen_WindowHandle_ReturnsCorrectState()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        Assert.False(manager.IsOpen(handle));

        manager.Open(handle);
        Assert.True(manager.IsOpen(handle));

        manager.Close(handle);
        Assert.False(manager.IsOpen(handle));
    }

    [Fact]
    public void Get_WindowHandle_ReturnsEntry()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        Assert.Null(manager.Get(handle));

        var opened = manager.Open(handle);
        var got = manager.Get(handle);

        Assert.Same(opened, got);
    }

    [Fact]
    public void BringToFront_WindowHandle_BringsToFront()
    {
        var manager = new WindowManager();

        var handle1 = manager.Window(w => new TextBlockWidget("1")).Title("One");
        var handle2 = manager.Window(w => new TextBlockWidget("2")).Title("Two");

        var entry1 = manager.Open(handle1);
        var entry2 = manager.Open(handle2);

        // entry2 is now active (last opened)
        Assert.Same(entry2, manager.ActiveWindow);

        // Bring entry1 to front
        manager.BringToFront(handle1);

        Assert.Same(entry1, manager.ActiveWindow);
    }

    [Fact]
    public void WindowContentContext_ProvidesWindowProperty()
    {
        var manager = new WindowManager();
        WindowHandle? capturedWindow = null;

        var handle = manager.Window(w => {
            capturedWindow = w.Window;
            return new TextBlockWidget("Hello");
        }).Title("Test");

        // Manually build content to verify the Window property is accessible
        // (In real usage, this happens during reconciliation)
        var entry = manager.Open(handle);
        
        // Build content explicitly to trigger the content builder
        // This simulates what happens during reconciliation
        var windowContext = new WindowContentContext<Hex1bWidget>(handle);
        Assert.Same(handle, windowContext.Window);
    }

    [Fact]
    public void CloseAll_ClearsHandleMappings()
    {
        var manager = new WindowManager();

        var handle1 = manager.Window(w => new TextBlockWidget("1")).Title("One");
        var handle2 = manager.Window(w => new TextBlockWidget("2")).Title("Two");

        manager.Open(handle1);
        manager.Open(handle2);

        manager.CloseAll();

        Assert.False(manager.IsOpen(handle1));
        Assert.False(manager.IsOpen(handle2));
        Assert.Equal(0, manager.Count);
    }

    #endregion
}
