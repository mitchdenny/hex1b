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

    [Fact]
    public async Task Picker_StackedPickers_OpenedDropdown_DoesNotBleedSiblingChipBackground()
    {
        // Reproduces the bug where a Picker dropdown's transparent inner cells
        // leak whatever was painted underneath. With two pickers stacked
        // vertically and the dropdown opened, the user's reported failure
        // mode is that the SECOND item in the dropdown ("Banana") wears the
        // resting chip background of the second Picker that sits behind it.
        //
        // Reproduction recipe (from the user's screenshot in samples/PickerDemo):
        //   1. Two pickers separated by a spacer so the second picker chip
        //      lands at the SAME terminal Y as the second dropdown item
        //      ("Banana"), not a border row.
        //   2. Open with DownArrow — picker opens with "Banana" pre-selected
        //      so the chip behind it is masked by the white selected row.
        //   3. Press DownArrow again — selection moves to "Cherry". The
        //      previously-selected "Banana" row loses its white background
        //      and reveals what's underneath. THIS is the user-visible bug.
        //
        // Expectation after fix: every cell that falls inside the dropdown's
        // bounding box stays painted by the dropdown's BackgroundPanel, even
        // after the selected row moves away.
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new PickerWidget(["Apple", "Banana", "Cherry"]),
                new TextBlockWidget(""),
                new TextBlockWidget(""),
                new PickerWidget(["Xenon", "Yttrium", "Zirconium"]),
            ]))
            .WithHeadless()
            .WithDimensions(40, 12)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple ▼") && s.ContainsText("Xenon ▼"),
                TimeSpan.FromSeconds(5), "both pickers to render")
            // DownArrow on a closed picker: open with the NEXT item (Banana)
            // pre-selected — this matches what the user does to trigger the bug.
            .Down()
            .WaitUntil(s => s.ContainsText("Apple") && s.ContainsText("Banana") && s.ContainsText("Cherry"),
                TimeSpan.FromSeconds(5), "first picker dropdown to open with Banana selected")
            // Move selection from Banana to Cherry. Banana's row now has to be
            // re-painted as overlay background instead of the selected white.
            .Down()
            .WaitUntil(s => s.GetText().Contains("> Cherry"),
                TimeSpan.FromSeconds(2), "selection moved to Cherry")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        using var snapshot = terminal.CreateSnapshot();

        // Locate the dropdown by finding the popup-side "Banana" cell — the
        // one NOT on row 1 (which is where the second picker's "Xenon ▼" sits).
        (int Line, int Column)? popupBanana = null;
        foreach (var match in snapshot.FindText("Banana"))
        {
            if (match.Line >= 2)
            {
                popupBanana = match;
                break;
            }
        }
        Assert.True(popupBanana.HasValue,
            $"Expected to find Banana inside the popup body.\nScreen:\n{snapshot.GetText()}");

        // The dropdown wraps a ListWidget in a BorderWidget. The "Banana"
        // text starts after the border (col -1) and the unselected indicator
        // (cols -3..-2). So the popup's left edge is at popupBanana.Column - 3
        // and its top edge sits one row above whichever item rendered first
        // in the popup ("Apple" at popupBanana.Line - 1, then border at
        // popupBanana.Line - 2).
        var popupLeft = popupBanana.Value.Column - 3;
        var popupTop = popupBanana.Value.Line - 2;

        // Resolve the colour we expect NOT to see anywhere inside the popup
        // bounding box. With the default theme this is the resting Picker
        // chip background — rgb(60, 60, 60) — that was painted by the
        // second picker before the dropdown rendered on top of it.
        var theme = Hex1b.Theming.Hex1bThemes.Default;
        var siblingChipBg = theme.Get(Hex1b.Theming.ButtonTheme.BackgroundColor);

        // Walk the dropdown's full bounding box (border + items + bottom
        // border). Anything wearing the sibling picker's chip colour means
        // a cell wasn't painted by the popup.
        var leakedCells = new List<(int X, int Y, Hex1b.Theming.Hex1bColor? Bg)>();
        for (int y = popupTop; y < popupTop + 5; y++)
        {
            for (int x = popupLeft; x < popupLeft + 12; x++)
            {
                if (x < 0 || y < 0 || x >= snapshot.Width || y >= snapshot.Height)
                {
                    continue;
                }
                var bg = snapshot.GetCell(x, y).Background;
                if (bg.HasValue && bg.Value.Equals(siblingChipBg))
                {
                    leakedCells.Add((x, y, bg));
                }
            }
        }

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(
            leakedCells.Count == 0,
            $"Sibling picker chip background ({siblingChipBg}) bled through the open dropdown at {leakedCells.Count} cell(s): " +
            string.Join(", ", leakedCells.Take(10).Select(c => $"({c.X},{c.Y})")) +
            $".\nScreen:\n{snapshot.GetText()}");
    }
}
