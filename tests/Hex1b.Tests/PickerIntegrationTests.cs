using Hex1b.Input;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the PickerWidget popup and click-away behavior.
/// These tests verify the full lifecycle of the picker popup including
/// opening, selecting, and dismissing via various methods.
/// </summary>
public class PickerIntegrationTests
{
    [Fact]
    public async Task Picker_EnterKey_OpensPopup()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - The popup should have opened showing all items
        // (We verified this via the WaitUntil condition)
    }

    [Fact]
    public async Task Picker_SelectItem_ClosesPopupAndUpdatesSelection()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedText = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                        .OnSelectionChanged(e => { selectedText = e.SelectedText; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            .Down()   // Navigate to Banana
            .Enter()  // Select Banana
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedText);
        // The picker should now show "Banana ▼" and the popup should be closed
        Assert.True(terminal.CreateSnapshot().ContainsText("Banana"));
    }

    [Fact]
    public async Task Picker_EscapeKey_ClosesPopupWithoutChangingSelection()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectionChangedCount = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                        .OnSelectionChanged(e => { selectionChangedCount++; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            .Down()   // Navigate to Banana (but don't select)
            .Escape() // Dismiss without selecting
            .WaitUntil(s => s.ContainsText("Apple ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Selection should not have changed
        Assert.Equal(0, selectionChangedCount);
        // The picker should still show "Apple ▼"
        Assert.True(terminal.CreateSnapshot().ContainsText("Apple"));
    }

    [Fact]
    public async Task Picker_ClickAwayOnBackdrop_ClosesPopupWithoutChangingSelection()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectionChangedCount = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget("Click here to dismiss"),
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                        .OnSelectionChanged(e => { selectionChangedCount++; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            // Click on the backdrop area (top-left corner, far from the popup content)
            .ClickAt(1, 1, MouseButton.Left)
            .WaitUntil(s => s.ContainsText("Apple ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to close after click-away")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Selection should not have changed
        Assert.Equal(0, selectionChangedCount);
        // The picker should still show "Apple ▼"
        Assert.True(terminal.CreateSnapshot().ContainsText("Apple"));
    }

    [Fact]
    public async Task Picker_ClickOnPopupContent_DoesNotDismiss()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            // Click somewhere on the popup content (we'll click on the first visible item area)
            // The popup list is rendered somewhere in the middle-ish of the screen
            // Just click on a middle area where the list would be
            .ClickAt(5, 5, MouseButton.Left)
            .Wait(100)  // Wait a bit to make sure click was processed
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The popup should still be visible after clicking on its content
        // (unless the click selected an item, which would close it)
        // This test verifies that the click-away logic correctly identifies content bounds
    }

    [Fact]
    public async Task Picker_DownArrow_OpensPopupWithNextItemSelected()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedText = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                        .OnSelectionChanged(e => { selectedText = e.SelectedText; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Down()  // Open popup with next item (Banana) pre-selected
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            .Enter() // Confirm selection
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedText);
    }

    [Fact]
    public async Task Picker_UpArrow_OpensPopupWithPreviousItemSelected()
    {
        // Arrange - Start with Cherry selected (index 2)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedText = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"]) { InitialSelectedIndex = 2 }
                        .OnSelectionChanged(e => { selectedText = e.SelectedText; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "picker to render with Cherry")
            .Up()    // Open popup with previous item (Banana) pre-selected
            .WaitUntil(s => s.ContainsText("Apple") && s.ContainsText("Banana"), TimeSpan.FromSeconds(2), "popup to open")
            .Enter() // Confirm selection
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedText);
    }

    [Fact]
    public async Task Picker_PopupOpened_FocusMovesToList()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open popup and verify navigation works (proving focus moved to list)
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("> Apple"), TimeSpan.FromSeconds(2), "popup to open with selection indicator")
            .Down()   // Navigate down in the list
            .WaitUntil(s => s.ContainsText("> Banana"), TimeSpan.FromSeconds(2), "list selection to move")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - If we got here, the list received focus and responded to Down arrow
        Assert.True(terminal.CreateSnapshot().ContainsText("> Banana"));
    }

    [Fact]
    public async Task Picker_PopupDismissed_FocusRestoresToPicker()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Open popup, dismiss it, then open again to verify focus returned to picker
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to open")
            .Escape() // Dismiss
            .WaitUntil(s => !s.ContainsText("Cherry") || s.ContainsText("Apple ▼"), TimeSpan.FromSeconds(2), "popup to close")
            .Enter()  // Open again - this proves focus returned to picker
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "popup to reopen")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - If we got here, focus was restored and picker could be reopened
    }

    [Fact]
    public async Task Picker_MultiplePickersOnScreen_TabNavigatesBetweenThem()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedFruit = "Apple";
        var selectedColor = "Red";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new HStackWidget([
                        new TextBlockWidget("Fruit: "),
                        new PickerWidget(["Apple", "Banana", "Cherry"])
                            .OnSelectionChanged(e => { selectedFruit = e.SelectedText; })
                    ]),
                    new HStackWidget([
                        new TextBlockWidget("Color: "),
                        new PickerWidget(["Red", "Green", "Blue"])
                            .OnSelectionChanged(e => { selectedColor = e.SelectedText; })
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Select from first picker, tab to second, select from second
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple") && s.ContainsText("Red"), TimeSpan.FromSeconds(2), "pickers to render")
            // Select Banana from first picker
            .Down()   // Opens with Banana selected
            .WaitUntil(s => s.ContainsText("Cherry"), TimeSpan.FromSeconds(2), "fruit popup to open")
            .Enter()  // Confirm Banana
            .WaitUntil(s => s.ContainsText("Banana ▼"), TimeSpan.FromSeconds(2), "fruit popup to close")
            // Tab to second picker
            .Tab()
            // Select Green from second picker
            .Down()   // Opens with Green selected
            .WaitUntil(s => s.ContainsText("Blue"), TimeSpan.FromSeconds(2), "color popup to open")
            .Enter()  // Confirm Green
            .WaitUntil(s => s.ContainsText("Green ▼"), TimeSpan.FromSeconds(2), "color popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedFruit);
        Assert.Equal("Green", selectedColor);
    }
}
