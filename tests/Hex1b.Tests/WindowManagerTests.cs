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
            id: "test-window",
            title: "Test Window",
            content: () => new TextBlockWidget("Hello"),
            width: 40,
            height: 15
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
            id: "test-window",
            title: "Window 1",
            content: () => new TextBlockWidget("One")
        );

        var entry2 = manager.Open(
            id: "test-window",
            title: "Window 2",
            content: () => new TextBlockWidget("Two")
        );

        Assert.Same(entry1, entry2);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Close_RemovesWindow()
    {
        var manager = new WindowManager();
        var entry = manager.Open(
            id: "test-window",
            title: "Test",
            content: () => new TextBlockWidget("Hello")
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
            id: "test-window",
            title: "Test",
            content: () => new TextBlockWidget("Hello")
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
            id: "test-window",
            title: "Test",
            content: () => new TextBlockWidget("Hello"),
            onClose: () => callbackInvoked = true
        );

        manager.Close(entry);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void CloseAll_RemovesAllWindows()
    {
        var manager = new WindowManager();
        manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));
        manager.Open("win3", "Window 3", () => new TextBlockWidget("3"));

        manager.CloseAll();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void CloseAll_InvokesAllCallbacks()
    {
        var manager = new WindowManager();
        var callbackCount = 0;

        manager.Open("win1", "Window 1", () => new TextBlockWidget("1"), onClose: () => callbackCount++);
        manager.Open("win2", "Window 2", () => new TextBlockWidget("2"), onClose: () => callbackCount++);

        manager.CloseAll();

        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public void BringToFront_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

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
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

        Assert.Same(entry2, manager.ActiveWindow);

        manager.BringToFront(entry1);

        Assert.Same(entry1, manager.ActiveWindow);
    }

    [Fact]
    public void ActiveWindow_PrefersModalWindow()
    {
        var manager = new WindowManager();
        var normalWindow = manager.Open("normal", "Normal", () => new TextBlockWidget("Normal"));
        var modalWindow = manager.Open("modal", "Modal", () => new TextBlockWidget("Modal"), isModal: true);

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

        manager.Open("normal", "Normal", () => new TextBlockWidget("Normal"));
        Assert.False(manager.HasModalWindow);

        manager.Open("modal", "Modal", () => new TextBlockWidget("Modal"), isModal: true);
        Assert.True(manager.HasModalWindow);
    }

    [Fact]
    public void Get_ReturnsEntryById()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));

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
        manager.Open("test", "Test", () => new TextBlockWidget("Hello"));

        Assert.True(manager.IsOpen("test"));
        Assert.False(manager.IsOpen("other"));
    }

    [Fact]
    public void All_ReturnsWindowsInZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));
        var entry3 = manager.Open("win3", "Window 3", () => new TextBlockWidget("3"));

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

        manager.Open("test", "Test", () => new TextBlockWidget("Hello"));

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnClose()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.Close(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void Changed_EventRaisedOnBringToFront()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.BringToFront(entry);

        Assert.True(eventRaised);
    }

    [Fact]
    public void WindowEntry_CloseMethod_ClosesWindow()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));

        entry.Close();

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void WindowEntry_BringToFrontMethod_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

        entry1.BringToFront();

        Assert.True(entry1.ZIndex > entry2.ZIndex);
    }

    [Fact]
    public void BringToFront_InvokesOnActivatedCallback()
    {
        var manager = new WindowManager();
        var activated = false;
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"), 
            onActivated: () => activated = true);

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
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"), 
            onDeactivated: () => deactivated = true);
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

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
        var entry = manager.Open("win", "Window", () => new TextBlockWidget("Hello"),
            onActivated: () => activatedCount++);

        // Calling BringToFront on already-active window should not trigger callback
        entry.BringToFront();
        entry.BringToFront();
        entry.BringToFront();

        Assert.Equal(0, activatedCount);
    }
}
