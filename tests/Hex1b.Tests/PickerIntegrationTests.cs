using Hex1b.Input;
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
        // Arrange & Act
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - The popup should have opened showing all items
        Assert.True(snapshot.ContainsText("Banana"));
        Assert.True(snapshot.ContainsText("Cherry"));
    }

    [Fact]
    public async Task Picker_SelectItem_ClosesPopupAndUpdatesSelection()
    {
        // Arrange
        var selectedText = "";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
                    .OnSelectionChanged(e => { selectedText = e.SelectedText; })
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Down()   // Navigate to Banana
            .Enter()  // Select Banana
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to close")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedText);
        // The picker should now show "Banana ▼" and the popup should be closed
        Assert.True(snapshot.ContainsText("Banana"));
    }

    [Fact]
    public async Task Picker_EscapeKey_ClosesPopupWithoutChangingSelection()
    {
        // Arrange
        var selectionChangedCount = 0;
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
                    .OnSelectionChanged(e => { selectionChangedCount++; })
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Down()   // Navigate to Banana (but don't select)
            .Escape() // Dismiss without selecting
            .WaitUntil(s => s.ContainsText("Apple ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to close")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Selection should not have changed
        Assert.Equal(0, selectionChangedCount);
        // The picker should still show "Apple ▼"
        Assert.True(snapshot.ContainsText("Apple"));
    }

    [Fact]
    public async Task Picker_ClickAwayOnBackdrop_ClosesPopupWithoutChangingSelection()
    {
        // Arrange
        var selectionChangedCount = 0;
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                options.EnableMouse = true;
                return ctx => new VStackWidget([
                    new TextBlockWidget("Click here to dismiss"),
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                        .OnSelectionChanged(e => { selectionChangedCount++; })
                ]);
            })
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            // Click on the backdrop area (top-left corner, far from the popup content)
            .ClickAt(1, 1, MouseButton.Left)
            .WaitUntil(s => s.ContainsText("Apple ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to close after click-away")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Selection should not have changed
        Assert.Equal(0, selectionChangedCount);
        // The picker should still show "Apple ▼"
        Assert.True(snapshot.ContainsText("Apple"));
    }

    [Fact]
    public async Task Picker_ClickOnPopupContent_DoesNotDismiss()
    {
        // Arrange
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                options.EnableMouse = true;
                return ctx => new VStackWidget([
                    new PickerWidget(["Apple", "Banana", "Cherry"])
                ]);
            })
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        // Open the popup and wait for the list content to render.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        using var popupSnapshot = terminal.CreateSnapshot();
        (int Line, int Column)? popupApple = null;
        foreach (var match in popupSnapshot.FindText("Apple"))
        {
            if (match.Line > 0)
            {
                popupApple = match;
                break;
            }
        }

        Assert.True(
            popupApple.HasValue,
            $"Expected to find the popup's selected Apple item.\nScreen:\n{popupSnapshot.GetText()}");

        var popupBorderX = popupApple.Value.Column;
        var popupBorderY = popupApple.Value.Line - 1;
        Assert.True(
            popupBorderY >= 0,
            $"Expected popup border row above selected item.\nScreen:\n{popupSnapshot.GetText()}");

        // Click the popup border itself. If that click wrongly dismisses the popup, the
        // subsequent Enter will reopen it instead of selecting the focused item.
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(popupBorderX, popupBorderY, MouseButton.Left)
            .Enter()
            .WaitUntil(s => s.ContainsText("Apple ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to close after selecting current item")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        using var finalSnapshot = terminal.CreateSnapshot();

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(finalSnapshot.ContainsText("Apple ▼"));
        Assert.False(finalSnapshot.ContainsText("Cherry"));
    }

    [Fact]
    public async Task Picker_DownArrow_OpensPopupWithNextItemSelected()
    {
        // Arrange
        var selectedText = "";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
                    .OnSelectionChanged(e => { selectedText = e.SelectedText; })
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Down()  // Open popup with next item (Banana) pre-selected
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Enter() // Confirm selection
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to close")
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
        var selectedText = "";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"]) { InitialSelectedIndex = 2 }
                    .OnSelectionChanged(e => { selectedText = e.SelectedText; })
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "picker to render with Cherry")
            .Up()    // Open popup with previous item (Banana) pre-selected
            .WaitUntil(s => s.ContainsText("Apple") && s.ContainsText("Banana"), TimeSpan.FromSeconds(5), "popup to open")
            .Enter() // Confirm selection
            .WaitUntil(s => s.ContainsText("Banana ▼") && !s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "popup to close")
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
        // Arrange & Act
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act - Open popup and verify navigation works (proving focus moved to list)
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("> Apple"), TimeSpan.FromSeconds(5), "popup to open with selection indicator")
            .Down()   // Navigate down in the list
            .WaitUntil(s => s.ContainsText("> Banana"), TimeSpan.FromSeconds(5), "list selection to move")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - If we got here, the list received focus and responded to Down arrow
        Assert.True(snapshot.ContainsText("> Banana"));
    }

    [Fact]
    public async Task Picker_PopupDismissed_FocusRestoresToPicker()
    {
        // Arrange & Act
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"])
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act - Open popup, dismiss it, then open again to verify focus returned to picker
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(5), "picker to render")
            .Enter()  // Open the picker popup
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to open")
            .Escape() // Dismiss
            .WaitUntil(s => !s.ContainsText("Cherry") || s.ContainsText("Apple ▼"), TimeSpan.FromSeconds(5), "popup to close")
            .Enter()  // Open again - this proves focus returned to picker
            .WaitUntil(s => s.ContainsText("Banana") && s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "popup to reopen")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Focus was restored and picker could be reopened
        Assert.True(snapshot.ContainsText("Banana"));
        Assert.True(snapshot.ContainsText("Cherry"));
    }

    [Fact]
    public async Task Picker_MultiplePickersOnScreen_TabNavigatesBetweenThem()
    {
        // Arrange
        var selectedFruit = "Apple";
        var selectedColor = "Red";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
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
            ]))
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Act - Select from first picker, tab to second, select from second
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple") && s.ContainsText("Red"), TimeSpan.FromSeconds(5), "pickers to render")
            // Select Banana from first picker
            .Down()   // Opens with Banana selected
            .WaitUntil(s => s.ContainsText("Cherry"), TimeSpan.FromSeconds(5), "fruit popup to open")
            .Enter()  // Confirm Banana
            .WaitUntil(s => s.ContainsText("Banana ▼"), TimeSpan.FromSeconds(5), "fruit popup to close")
            // Tab to second picker
            .Tab()
            // Select Green from second picker
            .Down()   // Opens with Green selected
            .WaitUntil(s => s.ContainsText("Blue"), TimeSpan.FromSeconds(5), "color popup to open")
            .Enter()  // Confirm Green
            .WaitUntil(s => s.ContainsText("Green ▼"), TimeSpan.FromSeconds(5), "color popup to close")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.Equal("Banana", selectedFruit);
        Assert.Equal("Green", selectedColor);
    }
}
