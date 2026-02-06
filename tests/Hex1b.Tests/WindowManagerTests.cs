using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowManager functionality.
/// </summary>
public class WindowManagerTests
{
    [Fact]
    public void Open_AddsWindowToManager()
    {
        var manager = new WindowManager();

        var entry = manager.Open(
            "test-window",
            "Test Window",
            _ => new TextBlockWidget("Hello"),
            new WindowOptions { Width = 40, Height = 15 }
        );

        Assert.NotNull(entry);
        Assert.Equal("test-window", entry.Id);
        Assert.Equal("Test Window", entry.Title);
        Assert.Equal(40, entry.Width);
        Assert.Equal(15, entry.Height);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Open_WithSameId_ReturnsSameEntry()
    {
        var manager = new WindowManager();

        var entry1 = manager.Open(
            "test-window",
            "Window 1",
            _ => new TextBlockWidget("One")
        );

        var entry2 = manager.Open(
            "test-window",
            "Window 2",
            _ => new TextBlockWidget("Two")
        );

        Assert.Same(entry1, entry2);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Close_RemovesWindow()
    {
        var manager = new WindowManager();
        var entry = manager.Open(
            "test-window",
            "Test",
            _ => new TextBlockWidget("Hello")
        );

        var result = manager.Close(entry);

        Assert.True(result);
        Assert.Equal(0, manager.Count);
        Assert.False(manager.IsOpen("test-window"));
    }

    [Fact]
    public void Close_ById_RemovesWindow()
    {
        var manager = new WindowManager();
        manager.Open(
            "test-window",
            "Test",
            _ => new TextBlockWidget("Hello")
        );

        var result = manager.Close("test-window");

        Assert.True(result);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Close_InvokesOnCloseCallback()
    {
        var manager = new WindowManager();
        var callbackInvoked = false;

        var entry = manager.Open(
            "test-window",
            "Test",
            _ => new TextBlockWidget("Hello"),
            new WindowOptions { OnClose = () => callbackInvoked = true }
        );

        manager.Close(entry);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void CloseAll_RemovesAllWindows()
    {
        var manager = new WindowManager();
        manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));
        manager.Open("win3", "Window 3", _ => new TextBlockWidget("3"));

        manager.CloseAll();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void CloseAll_InvokesAllCallbacks()
    {
        var manager = new WindowManager();
        var callbackCount = 0;

        manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"), new WindowOptions { OnClose = () => callbackCount++ });
        manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"), new WindowOptions { OnClose = () => callbackCount++ });

        manager.CloseAll();

        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public void BringToFront_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));

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
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));

        Assert.Same(entry2, manager.ActiveWindow);

        manager.BringToFront(entry1);

        Assert.Same(entry1, manager.ActiveWindow);
    }

    [Fact]
    public void ActiveWindow_PrefersModalWindow()
    {
        var manager = new WindowManager();
        var normalWindow = manager.Open("normal", "Normal", _ => new TextBlockWidget("Normal"));
        var modalWindow = manager.Open("modal", "Modal", _ => new TextBlockWidget("Modal"), new WindowOptions { IsModal = true });

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

        manager.Open("normal", "Normal", _ => new TextBlockWidget("Normal"));
        Assert.False(manager.HasModalWindow);

        manager.Open("modal", "Modal", _ => new TextBlockWidget("Modal"), new WindowOptions { IsModal = true });
        Assert.True(manager.HasModalWindow);
    }

    [Fact]
    public void Get_ReturnsEntryById()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));

        var found = manager.Get("test");

        Assert.Same(entry, found);
    }

    [Fact]
    public void Get_ReturnsNullForUnknownId()
    {
        var manager = new WindowManager();

        var found = manager.Get("nonexistent");

        Assert.Null(found);
    }

    [Fact]
    public void IsOpen_ReturnsTrueForOpenWindow()
    {
        var manager = new WindowManager();
        manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));

        Assert.True(manager.IsOpen("test"));
        Assert.False(manager.IsOpen("other"));
    }

    [Fact]
    public void All_ReturnsWindowsInZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));
        var entry3 = manager.Open("win3", "Window 3", _ => new TextBlockWidget("3"));

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

        manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnClose()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.Close(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnBringToFront()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.BringToFront(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void WindowEntry_CloseMethod_ClosesWindow()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", _ => new TextBlockWidget("Hello"));

        entry.Close();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void WindowEntry_BringToFrontMethod_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));

        entry1.BringToFront();

        Assert.True(entry1.ZIndex > entry2.ZIndex);
    }

    [Fact]
    public void BringToFront_InvokesOnActivatedCallback()
    {
        var manager = new WindowManager();
        var activated = false;
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"), 
            new WindowOptions { OnActivated = () => activated = true });

        // entry2 should already be active since it was just opened
        // Now bring entry1 to front, then entry2 again
        entry1.BringToFront();
        activated = false;
        entry2.BringToFront();

        Assert.True(activated);
    }

    [Fact]
    public void BringToFront_InvokesOnDeactivatedCallback()
    {
        var manager = new WindowManager();
        var deactivated = false;
        var entry1 = manager.Open("win1", "Window 1", _ => new TextBlockWidget("1"), 
            new WindowOptions { OnDeactivated = () => deactivated = true });
        var entry2 = manager.Open("win2", "Window 2", _ => new TextBlockWidget("2"));

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
        var entry = manager.Open("win", "Window", _ => new TextBlockWidget("Hello"),
            new WindowOptions { OnActivated = () => activatedCount++ });

        // Calling BringToFront on already-active window should not trigger callback
        entry.BringToFront();
        entry.BringToFront();
        entry.BringToFront();

        Assert.Equal(0, activatedCount);
    }

    #region Phase 3: Title Bar and Actions Tests

    [Fact]
    public void Open_WithShowTitleBarFalse_SetsCorrectly()
    {
        var manager = new WindowManager();

        var entry = manager.Open(
            "frameless",
            "Frameless Window",
            _ => new TextBlockWidget("Content"),
            new WindowOptions { ShowTitleBar = false }
        );

        Assert.False(entry.ShowTitleBar);
    }

    [Fact]
    public void Open_DefaultsTitleBarActionsToCloseButton()
    {
        var manager = new WindowManager();

        var entry = manager.Open(
            "default",
            "Default Window",
            _ => new TextBlockWidget("Content")
        );

        Assert.Single(entry.RightTitleBarActions);
        Assert.Equal("×", entry.RightTitleBarActions[0].Icon);
    }

    [Fact]
    public void Open_WithCustomTitleBarActions_SetsCorrectly()
    {
        var manager = new WindowManager();
        var customActions = new[]
        {
            new WindowAction("?", _ => { }),
            WindowAction.Close()
        };

        var entry = manager.Open(
            "custom",
            "Custom Window",
            _ => new TextBlockWidget("Content"),
            new WindowOptions { RightTitleBarActions = customActions }
        );

        Assert.Equal(2, entry.RightTitleBarActions.Count);
        Assert.Equal("?", entry.RightTitleBarActions[0].Icon);
        Assert.Equal("×", entry.RightTitleBarActions[1].Icon);
    }

    [Fact]
    public void Open_WithEscapeBehavior_SetsCorrectly()
    {
        var manager = new WindowManager();

        var entry = manager.Open(
            "modal-like",
            "Modal Window",
            _ => new TextBlockWidget("Content"),
            new WindowOptions { EscapeBehavior = WindowEscapeBehavior.Ignore }
        );

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
        var entry = manager.Open("test", "Test", _ => new TextBlockWidget("Content"));
        var inputContext = new InputBindingActionContext(new FocusRing());
        var context = new WindowActionContext(entry, inputContext);

        context.Close();

        Assert.False(manager.IsOpen("test"));
    }

    #endregion

    #region Phase 4: Drag/Position Update Tests

    [Fact]
    public void UpdatePosition_ChangesEntryPosition()
    {
        var manager = new WindowManager();
        var entry = manager.Open("win", "Window", _ => new TextBlockWidget("Content"),
            new WindowOptions { X = 10, Y = 5 });

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
        var entry = manager.Open("win", "Window", _ => new TextBlockWidget("Content"));
        changedCount = 0;

        manager.UpdatePosition(entry, 50, 25);

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void UpdatePosition_AllowsNegativeValues()
    {
        // Negative values might be valid during drag before clamping
        var manager = new WindowManager();
        var entry = manager.Open("win", "Window", _ => new TextBlockWidget("Content"));

        manager.UpdatePosition(entry, -5, -3);

        Assert.Equal(-5, entry.X);
        Assert.Equal(-3, entry.Y);
    }

    #endregion

    #region Phase 6: Modal Result Tests

    [Fact]
    public async Task OpenModalAsync_CompletesWhenClosed()
    {
        var manager = new WindowManager();
        
        var task = manager.OpenModalAsync(
            "modal", "Modal", _ => new TextBlockWidget("Hello"),
            new WindowOptions { Width = 30, Height = 10 }
        );

        // Task should not be complete yet
        Assert.False(task.IsCompleted);

        // Close the modal
        manager.Close("modal");

        // Now task should complete
        await task;
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task OpenModalAsync_WithResult_ReturnsResult()
    {
        var manager = new WindowManager();
        
        var task = manager.OpenModalAsync<string>(
            "modal", "Modal", _ => new TextBlockWidget("Hello"),
            new WindowOptions { Width = 30, Height = 10 }
        );

        // Get the entry and close with result
        var entry = manager.Get("modal");
        entry?.CloseWithResult("success");

        var result = await task;
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task OpenModalAsync_WithResult_ClosedWithoutResult_ReturnsDefault()
    {
        var manager = new WindowManager();
        
        var task = manager.OpenModalAsync<int>(
            "modal", "Modal", _ => new TextBlockWidget("Hello"),
            new WindowOptions { Width = 30, Height = 10 }
        );

        // Close without result (simulates Escape key)
        manager.Close("modal");

        var result = await task;
        Assert.Equal(0, result); // Default for int
    }

    [Fact]
    public async Task OpenModalAsync_WithBoolResult()
    {
        var manager = new WindowManager();
        
        var task = manager.OpenModalAsync<bool>(
            "confirm", "Confirm", _ => new TextBlockWidget("Are you sure?"),
            new WindowOptions { Width = 30, Height = 10 }
        );

        var entry = manager.Get("confirm");
        entry?.CloseWithResult(true);

        var result = await task;
        Assert.True(result);
    }

    [Fact]
    public void CloseWithResult_CompletesResultSource()
    {
        var manager = new WindowManager();
        var tcs = new TaskCompletionSource<object?>();
        
        var entry = manager.Open(
            "modal", "Modal", _ => new TextBlockWidget("Hello"),
            new WindowOptions { IsModal = true }
        );
        entry.ResultSource = tcs;

        entry.CloseWithResult("test result");

        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal("test result", tcs.Task.Result);
    }

    [Fact]
    public void CloseWithResult_AlsoClosesWindow()
    {
        var manager = new WindowManager();
        
        var entry = manager.Open(
            "modal", "Modal", _ => new TextBlockWidget("Hello"),
            new WindowOptions { IsModal = true }
        );

        entry.CloseWithResult("done");

        Assert.Null(manager.Get("modal"));
        Assert.Equal(0, manager.Count);
    }

    #endregion
}
