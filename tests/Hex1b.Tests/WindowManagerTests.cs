using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowManager functionality using the fluent WindowHandle API.
/// </summary>
[TestClass]
public class WindowManagerTests
{
    [TestMethod]
    public void Open_AddsWindowToManager()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test Window")
            .Size(40, 15);
        
        var entry = manager.Open(handle);

        Assert.IsNotNull(entry);
        Assert.AreEqual("Test Window", entry.Title);
        Assert.AreEqual(40, entry.Width);
        Assert.AreEqual(15, entry.Height);
        Assert.AreEqual(1, manager.Count);
    }

    [TestMethod]
    public void Open_WithSameHandle_ReturnsSameEntry()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("One"))
            .Title("Window 1");

        var entry1 = manager.Open(handle);
        var entry2 = manager.Open(handle);

        Assert.AreSame(entry1, entry2);
        Assert.AreEqual(1, manager.Count);
    }

    [TestMethod]
    public void Close_RemovesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test");
        
        var entry = manager.Open(handle);

        var result = manager.Close(entry);

        Assert.IsTrue(result);
        Assert.AreEqual(0, manager.Count);
        Assert.IsFalse(manager.IsOpen(handle));
    }

    [TestMethod]
    public void Close_ByHandle_RemovesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test");
        
        manager.Open(handle);

        var result = manager.Close(handle);

        Assert.IsTrue(result);
        Assert.AreEqual(0, manager.Count);
    }

    [TestMethod]
    public void Close_InvokesOnCloseCallback()
    {
        var manager = new WindowManager();
        var callbackInvoked = false;

        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test")
            .OnClose(() => callbackInvoked = true);
        
        manager.Open(handle);
        manager.Close(handle);

        Assert.IsTrue(callbackInvoked);
    }

    [TestMethod]
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

        Assert.AreEqual(0, manager.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(2, callbackCount);
    }

    [TestMethod]
    public void BringToFront_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        // Initially entry2 should have higher z-index
        Assert.IsTrue(entry2.ZIndex > entry1.ZIndex);

        // Bring entry1 to front
        manager.BringToFront(entry1);

        // Now entry1 should have higher z-index
        Assert.IsTrue(entry1.ZIndex > entry2.ZIndex);
    }

    [TestMethod]
    public void ActiveWindow_ReturnsTopmostWindow()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        Assert.AreSame(entry2, manager.ActiveWindow);

        manager.BringToFront(entry1);

        Assert.AreSame(entry1, manager.ActiveWindow);
    }

    [TestMethod]
    public void ActiveWindow_PrefersModalWindow()
    {
        var manager = new WindowManager();
        var normalHandle = manager.Window(_ => new TextBlockWidget("Normal")).Title("Normal");
        var modalHandle = manager.Window(_ => new TextBlockWidget("Modal")).Title("Modal").Modal();
        
        var normalWindow = manager.Open(normalHandle);
        var modalWindow = manager.Open(modalHandle);

        // Modal should be active even though it was opened second
        Assert.AreSame(modalWindow, manager.ActiveWindow);

        // Even after bringing normal to front, modal should still be active
        manager.BringToFront(normalWindow);
        Assert.AreSame(modalWindow, manager.ActiveWindow);
    }

    [TestMethod]
    public void HasModalWindow_ReturnsTrueWhenModalExists()
    {
        var manager = new WindowManager();
        Assert.IsFalse(manager.HasModalWindow);

        var normalHandle = manager.Window(_ => new TextBlockWidget("Normal")).Title("Normal");
        manager.Open(normalHandle);
        Assert.IsFalse(manager.HasModalWindow);

        var modalHandle = manager.Window(_ => new TextBlockWidget("Modal")).Title("Modal").Modal();
        manager.Open(modalHandle);
        Assert.IsTrue(manager.HasModalWindow);
    }

    [TestMethod]
    public void Get_ReturnsEntryByHandle()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);

        var found = manager.Get(handle);

        Assert.AreSame(entry, found);
    }

    [TestMethod]
    public void Get_ReturnsNullForUnknownHandle()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");

        var found = manager.Get(handle);

        Assert.IsNull(found);
    }

    [TestMethod]
    public void IsOpen_ReturnsTrueForOpenWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var otherHandle = manager.Window(_ => new TextBlockWidget("Other")).Title("Other");
        
        manager.Open(handle);

        Assert.IsTrue(manager.IsOpen(handle));
        Assert.IsFalse(manager.IsOpen(otherHandle));
    }

    [TestMethod]
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

        Assert.AreEqual(3, all.Count);
        // Should be in z-order (entry1 first, entry3 last)
        Assert.AreSame(entry1, all[0]);
        Assert.AreSame(entry2, all[1]);
        Assert.AreSame(entry3, all[2]);
    }

    [TestMethod]
    public void Changed_EventRaisedOnOpen()
    {
        var manager = new WindowManager();
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        manager.Open(handle);

        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void Changed_EventRaisedOnClose()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.Close(entry);

        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void Changed_EventRaisedOnBringToFront()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);
        
        var eventRaised = false;
        manager.Changed += () => eventRaised = true;

        manager.BringToFront(entry);

        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void WindowEntry_CloseMethod_ClosesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Hello")).Title("Test");
        var entry = manager.Open(handle);

        entry.Close();

        Assert.AreEqual(0, manager.Count);
    }

    [TestMethod]
    public void WindowEntry_BringToFrontMethod_UpdatesZOrder()
    {
        var manager = new WindowManager();
        var h1 = manager.Window(_ => new TextBlockWidget("1")).Title("Window 1");
        var h2 = manager.Window(_ => new TextBlockWidget("2")).Title("Window 2");
        
        var entry1 = manager.Open(h1);
        var entry2 = manager.Open(h2);

        entry1.BringToFront();

        Assert.IsTrue(entry1.ZIndex > entry2.ZIndex);
    }

    [TestMethod]
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

        Assert.IsTrue(activated);
    }

    [TestMethod]
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

        Assert.IsTrue(deactivated);
    }

    [TestMethod]
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

        Assert.AreEqual(0, activatedCount);
    }

    #region Title Bar and Actions Tests

    [TestMethod]
    public void NoTitleBar_SetsShowTitleBarFalse()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .NoTitleBar();
        
        var entry = manager.Open(handle);

        Assert.IsFalse(entry.ShowTitleBar);
    }

    [TestMethod]
    public void DefaultRightTitleActions_HasCloseButton()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Default Window");
        
        var entry = manager.Open(handle);

        TestSeq.Single(entry.RightTitleBarActions);
        Assert.AreEqual("×", entry.RightTitleBarActions[0].Icon);
    }

    [TestMethod]
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

        Assert.AreEqual(2, entry.RightTitleBarActions.Count);
        Assert.AreEqual("?", entry.RightTitleBarActions[0].Icon);
        Assert.AreEqual("×", entry.RightTitleBarActions[1].Icon);
    }

    [TestMethod]
    public void EscapeBehavior_SetsCorrectly()
    {
        var manager = new WindowManager();

        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Modal Window")
            .EscapeBehavior(WindowEscapeBehavior.Ignore);
        
        var entry = manager.Open(handle);

        Assert.AreEqual(WindowEscapeBehavior.Ignore, entry.EscapeBehavior);
    }

    [TestMethod]
    public void WindowAction_Close_CreatesCloseAction()
    {
        var closeAction = WindowAction.Close();

        Assert.AreEqual("×", closeAction.Icon);
        Assert.AreEqual("Close", closeAction.Tooltip);
    }

    [TestMethod]
    public void WindowAction_Close_WithCustomIcon_CreatesAction()
    {
        var closeAction = WindowAction.Close("X");

        Assert.AreEqual("X", closeAction.Icon);
    }

    [TestMethod]
    public void WindowActionContext_Close_ClosesWindow()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Test");
        var entry = manager.Open(handle);
        var inputContext = new InputBindingActionContext(new FocusRing());
        var context = new WindowActionContext(entry, inputContext);

        context.Close();

        Assert.IsFalse(manager.IsOpen(handle));
    }

    #endregion

    #region Drag/Position Update Tests

    [TestMethod]
    public void UpdatePosition_ChangesEntryPosition()
    {
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content"))
            .Title("Window")
            .Position(10, 5);
        
        var entry = manager.Open(handle);

        manager.UpdatePosition(entry, 20, 15);

        Assert.AreEqual(20, entry.X);
        Assert.AreEqual(15, entry.Y);
    }

    [TestMethod]
    public void UpdatePosition_RaisesChangedEvent()
    {
        var manager = new WindowManager();
        var changedCount = 0;
        manager.Changed += () => changedCount++;
        
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Window");
        var entry = manager.Open(handle);
        changedCount = 0;

        manager.UpdatePosition(entry, 50, 25);

        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public void UpdatePosition_AllowsNegativeValues()
    {
        // Negative values might be valid during drag before clamping
        var manager = new WindowManager();
        var handle = manager.Window(_ => new TextBlockWidget("Content")).Title("Window");
        var entry = manager.Open(handle);

        manager.UpdatePosition(entry, -5, -3);

        Assert.AreEqual(-5, entry.X);
        Assert.AreEqual(-3, entry.Y);
    }

    #endregion

    #region Modal Result Tests

    [TestMethod]
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

        Assert.AreEqual("test result", receivedValue);
        Assert.IsFalse(wasCancelled);
    }

    [TestMethod]
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

        Assert.IsTrue(wasCancelled);
    }

    [TestMethod]
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

        Assert.IsTrue(wasCancelled);
        Assert.IsNull(receivedValue);
        Assert.AreEqual(0, manager.Count);
    }

    [TestMethod]
    public void CloseWithResult_AlsoClosesWindow()
    {
        var manager = new WindowManager();
        
        var handle = manager.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal();
        
        var entry = manager.Open(handle);

        entry.CloseWithResult("done");

        Assert.IsNull(manager.Get(handle));
        Assert.AreEqual(0, manager.Count);
    }

    #endregion

    #region Fluent WindowHandle API Tests

    [TestMethod]
    public void Window_CreatesWindowHandle()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"));

        Assert.IsNotNull(handle);
    }

    [TestMethod]
    public void WindowHandle_Title_SetsTitle()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("My Window");

        var entry = manager.Open(handle);

        Assert.AreEqual("My Window", entry.Title);
    }

    [TestMethod]
    public void WindowHandle_Size_SetsDimensions()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .Size(60, 30);

        var entry = manager.Open(handle);

        Assert.AreEqual(60, entry.Width);
        Assert.AreEqual(30, entry.Height);
    }

    [TestMethod]
    public void WindowHandle_Position_SetsCoordinates()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .Position(10, 20);

        var entry = manager.Open(handle);

        Assert.AreEqual(10, entry.X);
        Assert.AreEqual(20, entry.Y);
    }

    [TestMethod]
    public void WindowHandle_Modal_SetsIsModal()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Modal Window")
            .Modal();

        var entry = manager.Open(handle);

        Assert.IsTrue(entry.IsModal);
        Assert.IsTrue(manager.HasModalWindow);
    }

    [TestMethod]
    public void WindowHandle_Resizable_SetsResizableWithConstraints()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Resizable")
            .Resizable(minWidth: 20, minHeight: 10, maxWidth: 100, maxHeight: 50);

        var entry = manager.Open(handle);

        Assert.IsTrue(entry.IsResizable);
        Assert.AreEqual(20, entry.MinWidth);
        Assert.AreEqual(10, entry.MinHeight);
        Assert.AreEqual(100, entry.MaxWidth);
        Assert.AreEqual(50, entry.MaxHeight);
    }

    [TestMethod]
    public void WindowHandle_NoTitleBar_HidesTitleBar()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .NoTitleBar();

        var entry = manager.Open(handle);

        Assert.IsFalse(entry.ShowTitleBar);
    }

    [TestMethod]
    public void WindowHandle_OnClose_SetsCallback()
    {
        var manager = new WindowManager();
        var callbackInvoked = false;

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test")
            .OnClose(() => callbackInvoked = true);

        var entry = manager.Open(handle);
        manager.Close(handle);

        Assert.IsTrue(callbackInvoked);
    }

    [TestMethod]
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

        Assert.AreEqual(2, entry.LeftTitleBarActions.Count);
        Assert.AreEqual("📌", entry.LeftTitleBarActions[0].Icon);
        Assert.AreEqual("📋", entry.LeftTitleBarActions[1].Icon);
    }

    [TestMethod]
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

        Assert.AreEqual(2, entry.RightTitleBarActions.Count);
        Assert.AreEqual("?", entry.RightTitleBarActions[0].Icon);
        Assert.AreEqual("×", entry.RightTitleBarActions[1].Icon);
    }

    [TestMethod]
    public void WindowHandle_DefaultRightTitleActions_HasCloseButton()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        var entry = manager.Open(handle);

        TestSeq.Single(entry.RightTitleBarActions);
        Assert.AreEqual("×", entry.RightTitleBarActions[0].Icon);
    }

    [TestMethod]
    public void Open_WindowHandle_BringsToFrontIfAlreadyOpen()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        var entry1 = manager.Open(handle);
        var entry2 = manager.Open(handle);

        Assert.AreSame(entry1, entry2);
        Assert.AreEqual(1, manager.Count);
    }

    [TestMethod]
    public void Close_WindowHandle_RemovesWindow()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        manager.Open(handle);
        var result = manager.Close(handle);

        Assert.IsTrue(result);
        Assert.AreEqual(0, manager.Count);
        Assert.IsFalse(manager.IsOpen(handle));
    }

    [TestMethod]
    public void IsOpen_WindowHandle_ReturnsCorrectState()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        Assert.IsFalse(manager.IsOpen(handle));

        manager.Open(handle);
        Assert.IsTrue(manager.IsOpen(handle));

        manager.Close(handle);
        Assert.IsFalse(manager.IsOpen(handle));
    }

    [TestMethod]
    public void Get_WindowHandle_ReturnsEntry()
    {
        var manager = new WindowManager();

        var handle = manager.Window(w => new TextBlockWidget("Hello"))
            .Title("Test");

        Assert.IsNull(manager.Get(handle));

        var opened = manager.Open(handle);
        var got = manager.Get(handle);

        Assert.AreSame(opened, got);
    }

    [TestMethod]
    public void BringToFront_WindowHandle_BringsToFront()
    {
        var manager = new WindowManager();

        var handle1 = manager.Window(w => new TextBlockWidget("1")).Title("One");
        var handle2 = manager.Window(w => new TextBlockWidget("2")).Title("Two");

        var entry1 = manager.Open(handle1);
        var entry2 = manager.Open(handle2);

        // entry2 is now active (last opened)
        Assert.AreSame(entry2, manager.ActiveWindow);

        // Bring entry1 to front
        manager.BringToFront(handle1);

        Assert.AreSame(entry1, manager.ActiveWindow);
    }

    [TestMethod]
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
        Assert.AreSame(handle, windowContext.Window);
    }

    [TestMethod]
    public void CloseAll_ClearsHandleMappings()
    {
        var manager = new WindowManager();

        var handle1 = manager.Window(w => new TextBlockWidget("1")).Title("One");
        var handle2 = manager.Window(w => new TextBlockWidget("2")).Title("Two");

        manager.Open(handle1);
        manager.Open(handle2);

        manager.CloseAll();

        Assert.IsFalse(manager.IsOpen(handle1));
        Assert.IsFalse(manager.IsOpen(handle2));
        Assert.AreEqual(0, manager.Count);
    }

    #endregion
}
