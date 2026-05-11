using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the SplitButtonWidget — primary action invocation,
/// dropdown popup lifecycle, in-popup keyboard navigation, and chip styling.
///
/// These tests verify the full user-facing contract end-to-end and pin the
/// intended behaviour observed in samples/FullAppDemo:
///
///   <code>
///   ctx.SplitButton()
///       .PrimaryAction("Action", _ => { /* primary */ })
///       .SecondaryAction("Option A", _ => { })
///       .SecondaryAction("Option B", _ => { })
///   </code>
///
/// The expected lifecycle is:
///   1. Initial render shows " Action ▼ " on the resting chip background.
///   2. Focus highlights the chip with the focused background colour.
///   3. DownArrow opens the dropdown popup with Option A pre-selected.
///   4. DownArrow inside the popup moves selection to Option B.
///   5. Enter activates the selected secondary action and dismisses the popup.
///   6. Focus returns to the SplitButton — pressing Enter again fires the
///      primary action.
///   7. Escape dismisses the popup without invoking any secondary action.
/// </summary>
public class SplitButtonIntegrationTests
{
    /// <summary>
    /// Constructs the canonical "Action / Option A / Option B" SplitButton
    /// used by every test below, capturing each handler's invocation count
    /// and the args delivered.
    /// </summary>
    private static SplitButtonWidget BuildButton(
        Action? onPrimary = null,
        Action<SplitButtonClickedEventArgs>? onOptionA = null,
        Action<SplitButtonClickedEventArgs>? onOptionB = null)
    {
        return new SplitButtonWidget()
            .PrimaryAction("Action", _ => onPrimary?.Invoke())
            .SecondaryAction("Option A", e => onOptionA?.Invoke(e))
            .SecondaryAction("Option B", e => onOptionB?.Invoke(e));
    }

    [Fact]
    public async Task SplitButton_InitialRender_ShowsPrimaryLabelAndDropdownArrow()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([BuildButton()]))
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("▼"), TimeSpan.FromSeconds(5), "split button label and arrow")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("Action", line);
        Assert.Contains("▼", line);
        // Width is " Action │ ▼ " = 12 cells (label 6 + chip pads 2 + arrow
        // region 4 [divider, space, arrow, trailing pad]). The leading and
        // trailing chip pads are part of the chip body, so cell 0 and cell
        // 11 are spaces.
        Assert.Equal(" ", snapshot.GetCell(0, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(11, 0).Character);
        // The divider sits at cell 8 (just past the primary region).
        Assert.Equal("│", snapshot.GetCell(8, 0).Character);
        // The dropdown arrow sits at cell 10 (between the divider's trailing
        // pad and the chip's trailing pad).
        Assert.Equal("▼", snapshot.GetCell(10, 0).Character);
    }

    [Fact]
    public async Task SplitButton_Focused_PaintsFocusedChipBackground()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([BuildButton()]))
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("▼"), TimeSpan.FromSeconds(5), "focused split button rendered")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var theme = new Hex1bTheme("Test");
        var expectedPrimaryBg = theme.Get(ButtonTheme.FocusedBackgroundColor);
        var expectedArrowBg = theme.Get(SplitButtonTheme.FocusedArrowBackgroundColor);

        // SplitButton is the only focusable in the tree, so it auto-focuses.
        // The primary region (cells 0–7: leading pad + "Action" + trailing
        // pad) sits on the focused primary background; the secondary
        // affordance region (cells 8–11: divider + space + arrow + trailing
        // pad) sits on the focused arrow background — a slightly dimmed
        // shade so the dropdown target reads as distinct.
        for (var x = 0; x <= 7; x++)
        {
            Assert.Equal(expectedPrimaryBg, snapshot.GetCell(x, 0).Background);
        }
        for (var x = 8; x <= 11; x++)
        {
            Assert.Equal(expectedArrowBg, snapshot.GetCell(x, 0).Background);
        }
    }

    [Fact]
    public async Task SplitButton_Unfocused_PaintsRestingChipBackground()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new ButtonWidget("Decoy"),
                BuildButton(),
            ]))
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Decoy") && s.ContainsText("Action") && s.ContainsText("▼"),
                TimeSpan.FromSeconds(5), "decoy button takes initial focus, split button rendered unfocused")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var theme = new Hex1bTheme("Test");
        var expectedPrimaryBg = theme.Get(ButtonTheme.BackgroundColor);
        var expectedArrowBg = theme.Get(SplitButtonTheme.ArrowBackgroundColor);

        // The Decoy button takes initial focus, so the SplitButton on row 1
        // is unfocused. Primary region (cells 0–7) sits on the resting
        // primary background; arrow region (cells 8–11) sits on the resting
        // arrow background (a half-shade darker than the primary chip).
        for (var x = 0; x <= 7; x++)
        {
            Assert.Equal(expectedPrimaryBg, snapshot.GetCell(x, 1).Background);
        }
        for (var x = 8; x <= 11; x++)
        {
            Assert.Equal(expectedArrowBg, snapshot.GetCell(x, 1).Background);
        }
    }

    [Fact]
    public async Task SplitButton_DownArrow_OpensDropdownWithBothOptionsVisible()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([BuildButton()]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Down() // open the dropdown
            .WaitUntil(s => s.ContainsText("Option A") && s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "dropdown popup to open showing both options")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Option A"));
        Assert.True(snapshot.ContainsText("Option B"));
        // ListWidget marks the active item with a "> " selection indicator —
        // Option A is pre-selected when the dropdown opens.
        Assert.True(snapshot.ContainsText("> Option A"),
            $"Expected Option A to be the initially selected item.\nScreen:\n{snapshot.GetText()}");
    }

    /// <summary>
    /// Regression for the user-reported bug: pressing DownArrow inside the
    /// open dropdown was failing to advance selection from Option A to Option
    /// B. The popup must respond to DownArrow so the user can navigate to
    /// secondary actions other than the first.
    /// </summary>
    [Fact]
    public async Task SplitButton_DropdownOpen_DownArrow_MovesSelectionToOptionB()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([BuildButton()]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Down() // open dropdown — Option A pre-selected
            .WaitUntil(s => s.ContainsText("> Option A"), TimeSpan.FromSeconds(5), "Option A initially selected")
            .Down() // navigate down to Option B
            .WaitUntil(s => s.ContainsText("> Option B"), TimeSpan.FromSeconds(5),
                "selection indicator should advance to Option B when DownArrow is pressed in the popup")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("> Option B"));
        // Option A should no longer be the active selection — the indicator
        // moved with the down-arrow press.
        Assert.False(snapshot.ContainsText("> Option A"),
            $"Expected the selection indicator to advance off Option A.\nScreen:\n{snapshot.GetText()}");
    }

    [Fact]
    public async Task SplitButton_DropdownOpen_EnterOnFirstItem_FiresOptionAHandler()
    {
        var optionACount = 0;
        var optionBCount = 0;
        var primaryCount = 0;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                BuildButton(
                    onPrimary: () => primaryCount++,
                    onOptionA: _ => optionACount++,
                    onOptionB: _ => optionBCount++)
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Down() // open dropdown
            .WaitUntil(s => s.ContainsText("> Option A"), TimeSpan.FromSeconds(5), "Option A initially selected")
            .Enter() // activate selected (Option A)
            .WaitUntil(s => !s.ContainsText("Option B"), TimeSpan.FromSeconds(5), "dropdown to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(1, optionACount);
        Assert.Equal(0, optionBCount);
        Assert.Equal(0, primaryCount);
    }

    /// <summary>
    /// Once the bug is fixed, navigating Down + Enter inside the popup must
    /// fire the second secondary action — not the first.
    /// </summary>
    [Fact]
    public async Task SplitButton_DropdownOpen_DownThenEnter_FiresOptionBHandler()
    {
        var optionACount = 0;
        var optionBCount = 0;
        var primaryCount = 0;
        SplitButtonClickedEventArgs? optionBArgs = null;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                BuildButton(
                    onPrimary: () => primaryCount++,
                    onOptionA: _ => optionACount++,
                    onOptionB: e => { optionBCount++; optionBArgs = e; })
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Down() // open dropdown
            .WaitUntil(s => s.ContainsText("> Option A"), TimeSpan.FromSeconds(5), "Option A initially selected")
            .Down() // move selection to Option B
            .WaitUntil(s => s.ContainsText("> Option B"), TimeSpan.FromSeconds(5), "Option B selected")
            .Enter() // activate selected (Option B)
            .WaitUntil(s => !s.ContainsText("Option B") || s.ContainsText("Action"),
                TimeSpan.FromSeconds(5), "dropdown to close after activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(1, optionBCount);
        Assert.Equal(0, optionACount);
        Assert.Equal(0, primaryCount);
        Assert.NotNull(optionBArgs);
        Assert.NotNull(optionBArgs!.Widget);
    }

    [Fact]
    public async Task SplitButton_PrimaryAction_FiresOnEnter_WithoutOpeningDropdown()
    {
        var optionACount = 0;
        var optionBCount = 0;
        var primaryCount = 0;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                BuildButton(
                    onPrimary: () => primaryCount++,
                    onOptionA: _ => optionACount++,
                    onOptionB: _ => optionBCount++)
            ]))
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Enter() // activate primary (no dropdown open)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(1, primaryCount);
        Assert.Equal(0, optionACount);
        Assert.Equal(0, optionBCount);
    }

    [Fact]
    public async Task SplitButton_DropdownOpen_Escape_DismissesWithoutFiringHandlers()
    {
        var optionACount = 0;
        var optionBCount = 0;
        var primaryCount = 0;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                BuildButton(
                    onPrimary: () => primaryCount++,
                    onOptionA: _ => optionACount++,
                    onOptionB: _ => optionBCount++)
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Down() // open dropdown
            .WaitUntil(s => s.ContainsText("Option A") && s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "dropdown to open")
            .Escape() // dismiss popup
            .WaitUntil(s => !s.ContainsText("Option A") && !s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "dropdown to close after Escape")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(0, optionACount);
        Assert.Equal(0, optionBCount);
        Assert.Equal(0, primaryCount);
        Assert.False(snapshot.ContainsText("Option A"));
        Assert.False(snapshot.ContainsText("Option B"));
        // The primary chip should still be visible after the dismiss.
        Assert.True(snapshot.ContainsText("Action"));
        Assert.True(snapshot.ContainsText("▼"));
    }

    /// <summary>
    /// Uber test that exercises the full SplitButton interaction surface in
    /// one sequence: render → focused chip styling → open dropdown via
    /// DownArrow → navigate Down to Option B → Enter to activate Option B
    /// (regression for the user-reported bug) → confirm dropdown closes and
    /// focus returns to the SplitButton → press Enter again to fire the
    /// primary action.
    ///
    /// Verifies, in order:
    ///   • " Action ▼ " label and arrow are visible on initial render.
    ///   • The full chip body sits on the focused background colour while
    ///     focused.
    ///   • DownArrow opens the dropdown with Option A pre-selected.
    ///   • DownArrow inside the popup advances the selection to Option B.
    ///   • Enter fires Option B (and only Option B) with the right args.
    ///   • The popup closes after activation.
    ///   • Focus returns to the SplitButton — pressing Enter once more fires
    ///     the primary action without re-opening the dropdown.
    /// </summary>
    [Fact]
    public async Task SplitButton_FullScenario_OpensDropdown_NavigatesDown_FiresOptionB_ThenPrimary()
    {
        var optionACount = 0;
        var optionBCount = 0;
        var primaryCount = 0;
        SplitButtonClickedEventArgs? optionBArgs = null;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                BuildButton(
                    onPrimary: () => primaryCount++,
                    onOptionA: _ => optionACount++,
                    onOptionB: e => { optionBCount++; optionBArgs = e; })
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        // Phase 1 — initial render: capture the focused chip styling and
        // confirm primary label + arrow are visible.
        var initialSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("▼"),
                TimeSpan.FromSeconds(5), "split button rendered")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(initialSnapshot.ContainsText("Action"));
        Assert.True(initialSnapshot.ContainsText("▼"));
        Assert.Equal(" ", initialSnapshot.GetCell(0, 0).Character);
        Assert.Equal(" ", initialSnapshot.GetCell(11, 0).Character);
        Assert.Equal("│", initialSnapshot.GetCell(8, 0).Character);
        Assert.Equal("▼", initialSnapshot.GetCell(10, 0).Character);

        var theme = new Hex1bTheme("Test");
        var expectedPrimaryBg = theme.Get(ButtonTheme.FocusedBackgroundColor);
        var expectedArrowBg = theme.Get(SplitButtonTheme.FocusedArrowBackgroundColor);
        for (var x = 0; x <= 7; x++)
        {
            Assert.Equal(expectedPrimaryBg, initialSnapshot.GetCell(x, 0).Background);
        }
        for (var x = 8; x <= 11; x++)
        {
            Assert.Equal(expectedArrowBg, initialSnapshot.GetCell(x, 0).Background);
        }

        // Phase 2 — open dropdown, navigate down, activate Option B,
        // then press Enter again to fire the primary action. This proves
        // focus is restored to the SplitButton after the popup dismisses.
        var finalSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .Down() // open dropdown
            .WaitUntil(s => s.ContainsText("> Option A") && s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "dropdown opened with Option A pre-selected")
            .Down() // navigate to Option B
            .WaitUntil(s => s.ContainsText("> Option B"),
                TimeSpan.FromSeconds(5), "selection advanced to Option B")
            .Enter() // activate Option B
            .WaitUntil(s => !s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "dropdown closed after activating Option B")
            .Enter() // primary action — proves focus came back to the SplitButton
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(1, optionBCount);
        Assert.Equal(0, optionACount);
        Assert.Equal(1, primaryCount);
        Assert.NotNull(optionBArgs);
        Assert.NotNull(optionBArgs!.Widget);

        // The chip is back on screen with no popup overlay.
        Assert.True(finalSnapshot.ContainsText("Action"));
        Assert.True(finalSnapshot.ContainsText("▼"));
        Assert.False(finalSnapshot.ContainsText("Option A"));
        Assert.False(finalSnapshot.ContainsText("Option B"));
    }

    /// <summary>
    /// SplitButton with no secondary actions degrades to a plain Button-style
    /// chip — same layout as ButtonNode, no divider, no arrow, and no
    /// secondary-affordance tint anywhere on the chip.
    /// </summary>
    [Fact]
    public async Task SplitButton_NoSecondaryActions_RendersUniformChipWithoutDivider()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([
                new SplitButtonWidget().PrimaryAction("Action", _ => { })
            ]))
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(5), "split button rendered")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Chip body is " Action " — 8 cells, no divider, no arrow.
        Assert.True(snapshot.ContainsText("Action"));
        Assert.False(snapshot.ContainsText("▼"));
        Assert.False(snapshot.ContainsText("│"));
        Assert.Equal(" ", snapshot.GetCell(0, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(7, 0).Character);
        // The whole chip should be on the focused primary background — no
        // arrow tint anywhere.
        var theme = new Hex1bTheme("Test");
        var primaryBg = theme.Get(ButtonTheme.FocusedBackgroundColor);
        var arrowBg = theme.Get(SplitButtonTheme.FocusedArrowBackgroundColor);
        for (var x = 0; x <= 7; x++)
        {
            Assert.Equal(primaryBg, snapshot.GetCell(x, 0).Background);
        }
        Assert.NotEqual(arrowBg, snapshot.GetCell(7, 0).Background);
    }

    [Fact]
    public async Task SplitButton_StackedSplitButtons_OpenedDropdown_DoesNotBleedSiblingChipBackground()
    {
        // Companion to PickerIntegrationTests.Picker_StackedPickers_OpenedDropdown_DoesNotBleedSiblingChipBackground.
        // SplitButton's BuildDropdownContent uses the same BorderWidget(list)
        // pattern as Picker, so it shares the same bleed-through bug: any cell
        // the popup doesn't explicitly paint shows whatever was on the surface
        // before — including a sibling SplitButton's resting chip background.
        var topButton = BuildButton();
        var bottomButton = new SplitButtonWidget()
            .PrimaryAction("Other", _ => { })
            .SecondaryAction("Choice X", _ => { })
            .SecondaryAction("Choice Y", _ => { });

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new VStackWidget([topButton, bottomButton]))
            .WithHeadless()
            .WithDimensions(40, 12)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("Other"),
                TimeSpan.FromSeconds(5), "both split buttons to render")
            .Down()  // Open the focused (top) split button's dropdown
            .WaitUntil(s => s.ContainsText("Option A") && s.ContainsText("Option B"),
                TimeSpan.FromSeconds(5), "top split button dropdown to open")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        using var snapshot = terminal.CreateSnapshot();

        (int Line, int Column)? popupOptionA = null;
        foreach (var match in snapshot.FindText("Option A"))
        {
            if (match.Line >= 2)
            {
                popupOptionA = match;
                break;
            }
        }
        Assert.True(popupOptionA.HasValue,
            $"Expected to find Option A inside the popup body.\nScreen:\n{snapshot.GetText()}");

        var popupLeft = popupOptionA.Value.Column - 3;  // border + indicator
        var popupTop = popupOptionA.Value.Line - 1;     // border above first item

        var siblingChipBg = Hex1bThemes.Default.Get(ButtonTheme.BackgroundColor);

        var leakedCells = new List<(int X, int Y, Hex1bColor? Bg)>();
        for (int y = popupTop; y < popupTop + 4; y++)
        {
            for (int x = popupLeft; x < popupLeft + 14; x++)
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
            $"Sibling SplitButton chip background ({siblingChipBg}) bled through the open dropdown at {leakedCells.Count} cell(s): " +
            string.Join(", ", leakedCells.Take(10).Select(c => $"({c.X},{c.Y})")) +
            $".\nScreen:\n{snapshot.GetText()}");
    }
}
