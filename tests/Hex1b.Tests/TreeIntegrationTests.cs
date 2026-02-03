using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive integration tests for TreeWidget keyboard navigation, mouse interaction,
/// rendering in borders, clipping, and visual output verification.
/// </summary>
public class TreeIntegrationTests
{
    #region Test Matrix Constants
    
    // Tree guide characters for verification
    private const string UnicodeBranch = "├─";
    private const string UnicodeLastBranch = "└─";
    private const string UnicodeVertical = "│";
    private const string AsciiBranch = "+-";
    private const string AsciiLastBranch = "\\-";
    private const string AsciiVertical = "|";
    
    // Indicators
    private const string ExpandedIndicator = "▼";
    private const string CollapsedIndicator = "▶";
    private const string CheckboxChecked = "[x]";
    private const string CheckboxUnchecked = "[ ]";
    
    #endregion

    #region Helper Methods

    private static TreeWidget CreateSimpleTree()
    {
        return new TreeWidget([
            new TreeItemWidget("Root").WithIcon("📁").Expanded().WithChildren(
                new TreeItemWidget("Child 1").WithIcon("📄"),
                new TreeItemWidget("Child 2").WithIcon("📄").WithChildren(
                    new TreeItemWidget("Grandchild")
                ),
                new TreeItemWidget("Child 3").WithIcon("📄")
            )
        ]);
    }

    private static TreeWidget CreateMultiRootTree()
    {
        return new TreeWidget([
            new TreeItemWidget("Root A").Expanded().WithChildren(
                new TreeItemWidget("A1"),
                new TreeItemWidget("A2")
            ),
            new TreeItemWidget("Root B").WithChildren(
                new TreeItemWidget("B1")
            ),
            new TreeItemWidget("Root C")
        ]);
    }

    private static TreeWidget CreateDeepTree()
    {
        return new TreeWidget([
            new TreeItemWidget("Level 0").Expanded().WithChildren(
                new TreeItemWidget("Level 1").Expanded().WithChildren(
                    new TreeItemWidget("Level 2").Expanded().WithChildren(
                        new TreeItemWidget("Level 3").Expanded().WithChildren(
                            new TreeItemWidget("Level 4")
                        )
                    )
                )
            )
        ]);
    }

    private static bool IsFocused(IHex1bTerminalRegion snapshot, string itemText)
    {
        var positions = snapshot.FindText(itemText);
        if (positions.Count == 0) return false;
        
        var (line, column) = positions[0];
        var cell = snapshot.GetCell(column, line);
        
        // Check for white background (focused state from TreeTheme)
        return cell.Background is { R: 255, G: 255, B: 255 };
    }

    #endregion

    #region Basic Rendering Tests

    [Fact]
    public async Task Tree_RendersWithCorrectStructure()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateSimpleTree())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify tree structure
        Assert.True(snapshot.ContainsText("📁 Root"));
        Assert.True(snapshot.ContainsText("📄 Child 1"));
        Assert.True(snapshot.ContainsText("📄 Child 2"));
        Assert.True(snapshot.ContainsText("📄 Child 3"));
        
        // Verify expand indicator is shown for expanded root
        Assert.True(snapshot.ContainsText(ExpandedIndicator));
    }

    [Fact]
    public async Task Tree_RendersGuideLines_Unicode()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateMultiRootTree())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root A") && s.ContainsText("A1"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify Unicode guide lines are present
        Assert.True(snapshot.ContainsText(UnicodeBranch) || snapshot.ContainsText(UnicodeLastBranch), 
            "Should contain Unicode branch characters");
    }

    [Fact]
    public async Task Tree_RendersGuideLines_Ascii()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateMultiRootTree().WithGuideStyle(TreeGuideStyle.Ascii))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root A") && s.ContainsText("A1"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify ASCII guide lines are present
        Assert.True(snapshot.ContainsText(AsciiBranch) || snapshot.ContainsText(AsciiLastBranch),
            "Should contain ASCII branch characters");
    }

    [Fact]
    public async Task Tree_CollapsedItem_HidesChildren()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Root").WithChildren(  // Not expanded
                    new TreeItemWidget("Hidden Child")
                )
            ]))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Root"));
        Assert.False(snapshot.ContainsText("Hidden Child"), "Collapsed items should hide children");
        Assert.True(snapshot.ContainsText(CollapsedIndicator), "Should show collapsed indicator");
    }

    #endregion

    #region Keyboard Navigation Tests

    [Fact]
    public async Task Tree_DownArrow_MovesToNextItem()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateMultiRootTree())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root A"), TimeSpan.FromSeconds(2), "tree to render")
            .Down()  // Move from Root A to A1
            .Wait(50)
            .Capture("after_down")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // A1 should now be focused (visually highlighted)
        Assert.True(IsFocused(snapshot, "A1"), "A1 should be focused after Down arrow");
    }

    [Fact]
    public async Task Tree_UpArrow_MovesToPreviousItem()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateMultiRootTree())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root A"), TimeSpan.FromSeconds(2), "tree to render")
            .Down()  // Move to A1
            .Down()  // Move to A2
            .Up()    // Move back to A1
            .Wait(50)
            .Capture("after_up")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "A1"), "A1 should be focused after Up arrow");
    }

    [Fact]
    public async Task Tree_DownArrow_WrapsToFirst()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2"),
                new TreeItemWidget("Item 3")
            ]))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .Down()  // Item 2
            .Down()  // Item 3
            .Down()  // Wrap to Item 1
            .Wait(50)
            .Capture("after_wrap")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "Item 1"), "Should wrap to first item");
    }

    [Fact]
    public async Task Tree_UpArrow_WrapsToLast()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2"),
                new TreeItemWidget("Item 3")
            ]))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .Up()  // Wrap to Item 3
            .Wait(50)
            .Capture("after_wrap")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "Item 3"), "Should wrap to last item");
    }

    [Fact]
    public async Task Tree_RightArrow_ExpandsCollapsedItem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").WithChildren(
                    new TreeItemWidget("Child")
                )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent"), TimeSpan.FromSeconds(2), "tree to render")
            .Right()  // Expand
            .WaitUntil(s => s.ContainsText("Child"), TimeSpan.FromSeconds(2), "child to appear")
            .Capture("after_expand")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Child"), "Child should be visible after expand");
        Assert.True(snapshot.ContainsText(ExpandedIndicator), "Should show expanded indicator");
    }

    [Fact]
    public async Task Tree_LeftArrow_CollapsesExpandedItem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("Child")
                )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("Child"), TimeSpan.FromSeconds(2), "tree to render expanded")
            .Left()  // Collapse
            .WaitUntil(s => !s.ContainsText("Child"), TimeSpan.FromSeconds(2), "child to disappear")
            .Capture("after_collapse")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(snapshot.ContainsText("Child"), "Child should be hidden after collapse");
        Assert.True(snapshot.ContainsText(CollapsedIndicator), "Should show collapsed indicator");
    }

    [Fact]
    public async Task Tree_RightArrow_OnExpandedItem_MovesToFirstChild()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("First Child"),
                    new TreeItemWidget("Second Child")
                )
            ]))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("First Child"), TimeSpan.FromSeconds(2), "tree to render")
            .Right()  // Move to first child (already expanded)
            .Wait(50)
            .Capture("after_right")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "First Child"), "First Child should be focused");
    }

    [Fact]
    public async Task Tree_LeftArrow_OnChild_MovesToParent()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("Child")
                )
            ]))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("Child"), TimeSpan.FromSeconds(2), "tree to render")
            .Down()   // Move to Child
            .Left()   // Move to Parent
            .Wait(50)
            .Capture("after_left")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "Parent"), "Parent should be focused");
    }

    [Fact]
    public async Task Tree_Enter_ActivatesItem()
    {
        var activatedLabel = "";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2")
            ]).OnItemActivated(e => { activatedLabel = e.Item.Label; }))
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .Down()   // Move to Item 2
            .Enter()  // Activate
            .Wait(50)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Item 2", activatedLabel);
    }

    #endregion

    #region Multi-Select Tests

    [Fact]
    public async Task Tree_MultiSelect_SpaceTogglesSelection()
    {
        var selectedCount = 0;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2"),
                new TreeItemWidget("Item 3")
            ]).WithMultiSelect().OnSelectionChanged(e => { selectedCount = e.SelectedItems.Count; })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(CheckboxUnchecked), TimeSpan.FromSeconds(2), "tree to render with checkboxes")
            .Key(Hex1bKey.Spacebar)  // Select Item 1
            .Wait(50)
            .Down()
            .Key(Hex1bKey.Spacebar)  // Select Item 2
            .Wait(50)
            .Capture("after_selection")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(2, selectedCount);
        // Should have checked boxes for selected items
        Assert.True(snapshot.ContainsText(CheckboxChecked), "Should show checked checkbox");
    }

    [Fact]
    public async Task Tree_MultiSelect_SpaceTogglesOff()
    {
        var selectedCount = 0;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2")
            ]).WithMultiSelect().OnSelectionChanged(e => { selectedCount = e.SelectedItems.Count; })),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .Key(Hex1bKey.Spacebar)  // Select Item 1
            .Wait(50)
            .Key(Hex1bKey.Spacebar)  // Deselect Item 1
            .Wait(50)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(0, selectedCount);
    }

    [Fact]
    public async Task Tree_MultiSelect_RendersCheckboxes()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Unchecked Item"),
                new TreeItemWidget("Checked Item").Selected()
            ]).WithMultiSelect())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Unchecked Item"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText(CheckboxUnchecked), "Should show unchecked checkbox");
        Assert.True(snapshot.ContainsText(CheckboxChecked), "Should show checked checkbox");
    }

    #endregion

    #region Mouse Interaction Tests

    [Fact]
    public async Task Tree_MouseClick_SelectsItem()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                options.EnableMouse = true;
                return ctx => new TreeWidget([
                    new TreeItemWidget("Item 1"),
                    new TreeItemWidget("Item 2"),
                    new TreeItemWidget("Item 3")
                ]);
            })
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1") && s.ContainsText("Item 2") && s.ContainsText("Item 3"), 
                TimeSpan.FromSeconds(2), "tree to render")
            // Click on Item 2 (row 1, after the indicator)
            .ClickAt(5, 1, MouseButton.Left)
            .Wait(50)
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "Item 2"), "Item 2 should be focused after click");
    }

    #endregion

    #region Border Containment Tests

    [Fact]
    public async Task Tree_InBorder_RendersCorrectly()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
                b.Tree(
                    new TreeItemWidget("Root").Expanded().WithChildren(
                        new TreeItemWidget("Child 1"),
                        new TreeItemWidget("Child 2")
                    )
                )
            ], title: "My Tree"))
            .WithHeadless()
            .WithDimensions(40, 15)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("My Tree") && s.ContainsText("Root"), TimeSpan.FromSeconds(2), "bordered tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify border is present
        Assert.True(snapshot.ContainsText("My Tree"), "Border title should be visible");
        Assert.True(snapshot.ContainsText("┌") || snapshot.ContainsText("╭"), "Border top-left corner should be visible");
        Assert.True(snapshot.ContainsText("┐") || snapshot.ContainsText("╮"), "Border top-right corner should be visible");
        
        // Verify tree content is inside
        Assert.True(snapshot.ContainsText("Root"));
        Assert.True(snapshot.ContainsText("Child 1"));
        Assert.True(snapshot.ContainsText("Child 2"));
    }

    [Fact]
    public async Task Tree_InBorder_BorderCornersAtCorrectPositions()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
                b.Tree(
                    new TreeItemWidget("Item 1"),
                    new TreeItemWidget("Item 2")
                )
            ]))
            .WithHeadless()
            .WithDimensions(30, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Check top-left corner at (0,0)
        var topLeft = snapshot.GetCell(0, 0);
        Assert.True(topLeft.Character == "┌" || topLeft.Character == "╭", 
            $"Top-left should be border corner, got '{topLeft.Character}'");
        
        // Check top-right corner (find the right edge)
        var positions = snapshot.FindText("┐");
        if (positions.Count == 0)
        {
            positions = snapshot.FindText("╮");
        }
        Assert.NotEmpty(positions);
    }

    [Fact]
    public async Task Tree_InNestedBorders_RendersCorrectly()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
                b.Border(inner => [
                    inner.Tree(
                        new TreeItemWidget("Deep Item")
                    )
                ], title: "Inner")
            ], title: "Outer"))
            .WithHeadless()
            .WithDimensions(50, 15)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Outer") && s.ContainsText("Inner") && s.ContainsText("Deep Item"), 
                TimeSpan.FromSeconds(2), "nested borders to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Outer"));
        Assert.True(snapshot.ContainsText("Inner"));
        Assert.True(snapshot.ContainsText("Deep Item"));
    }

    #endregion

    #region Clipping Tests

    [Fact]
    public async Task Tree_Clipping_LongLabelsTruncated()
    {
        var longLabel = "This is a very long label that should be clipped when rendered in a narrow container";
        
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
                b.Tree(
                    new TreeItemWidget(longLabel)
                )
            ]).FixedWidth(30))
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This is"), TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The full label should NOT appear (it should be clipped)
        Assert.False(snapshot.ContainsText("narrow container"), "Long text should be clipped");
        // But the beginning should be visible
        Assert.True(snapshot.ContainsText("This is"), "Beginning of text should be visible");
    }

    [Fact]
    public async Task Tree_Clipping_ContentStaysWithinBorder()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
                h.Border(b => [
                    b.Tree(
                        new TreeItemWidget("Left Tree Item")
                    )
                ]).FixedWidth(20),
                h.Border(b => [
                    b.Text("Right Side")
                ]).FixedWidth(20)
            ]))
            .WithHeadless()
            .WithDimensions(50, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left Tree") && s.ContainsText("Right Side"), 
                TimeSpan.FromSeconds(2), "side-by-side content to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Find the position of "Right Side" - it should be in the right panel
        var rightPositions = snapshot.FindText("Right");
        Assert.NotEmpty(rightPositions);
        var (rightLine, rightColumn) = rightPositions[0];
        
        // The right content should be past column 20 (the left panel's width)
        Assert.True(rightColumn >= 18, $"Right content should be in right panel, but was at column {rightColumn}");
    }

    #endregion

    #region Tab Navigation Between Trees

    [Fact]
    public async Task Tree_TabNavigatesBetweenMultipleTrees()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
                h.Border(b => [
                    b.Tree(
                        new TreeItemWidget("Left Item 1"),
                        new TreeItemWidget("Left Item 2")
                    )
                ], title: "Left"),
                h.Border(b => [
                    b.Tree(
                        new TreeItemWidget("Right Item 1"),
                        new TreeItemWidget("Right Item 2")
                    )
                ], title: "Right")
            ]))
            .WithHeadless()
            .WithDimensions(60, 15)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        
        // First capture: Left tree has focus
        var captureLeft = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left Item 1") && s.ContainsText("Right Item 1"), 
                TimeSpan.FromSeconds(2), "trees to render")
            .Capture("left_focused")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Tab to right tree and capture
        var captureRight = await new Hex1bTerminalInputSequenceBuilder()
            .Tab()
            .Wait(50)
            .Capture("right_focused")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Left tree should have focus initially
        Assert.True(IsFocused(captureLeft, "Left Item 1"), "Left Item 1 should be initially focused");
        
        // After Tab, right tree should have focus
        Assert.True(IsFocused(captureRight, "Right Item 1"), "Right Item 1 should be focused after Tab");
    }

    #endregion

    #region Deep Tree Navigation

    [Fact]
    public async Task Tree_DeepNavigation_TraversesAllLevels()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => CreateDeepTree())
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Level 0") && s.ContainsText("Level 4"), 
                TimeSpan.FromSeconds(2), "deep tree to render")
            // Navigate all the way down
            .Down()  // Level 1
            .Down()  // Level 2
            .Down()  // Level 3
            .Down()  // Level 4
            .Wait(50)
            .Capture("at_level_4")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "Level 4"), "Level 4 should be focused after navigating down");
    }

    [Fact]
    public async Task Tree_DeepNavigation_LeftArrowNavigatesToParent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(CreateDeepTree()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Level 0") && s.ContainsText("Level 4"), 
                TimeSpan.FromSeconds(2), "deep tree to render")
            // Navigate to Level 4
            .Down().Down().Down().Down()
            // Left on Level 4 (leaf, no children) moves to parent Level 3
            .Left()
            .Wait(50)
            .Capture("at_level_3")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Level 4 has no children, so Left moves to parent Level 3
        Assert.True(IsFocused(snapshot, "Level 3"), "Level 3 should be focused after Left from Level 4 (move to parent)");
    }

    #endregion

    #region Guide Style Verification

    [Theory]
    [InlineData(TreeGuideStyle.Unicode, "├─", "└─")]
    [InlineData(TreeGuideStyle.Ascii, "+-", "\\-")]
    [InlineData(TreeGuideStyle.Bold, "┣━", "┗━")]
    [InlineData(TreeGuideStyle.Double, "╠═", "╚═")]
    public async Task Tree_GuideStyle_RendersCorrectCharacters(TreeGuideStyle style, string branch, string lastBranch)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Root").Expanded().WithChildren(
                    new TreeItemWidget("Child 1"),
                    new TreeItemWidget("Child 2")
                )
            ]).WithGuideStyle(style))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root") && s.ContainsText("Child 1") && s.ContainsText("Child 2"), 
                TimeSpan.FromSeconds(2), "tree to render")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // At least one of branch or lastBranch should be present
        Assert.True(snapshot.ContainsText(branch) || snapshot.ContainsText(lastBranch),
            $"Should contain {style} guide characters ('{branch}' or '{lastBranch}')");
    }

    #endregion

    #region Expand/Collapse Event Tests

    [Fact]
    public async Task Tree_OnExpanded_FiresEvent()
    {
        var expandedLabel = "";
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent")
                    .OnExpanded(e => { expandedLabel = e.Item.Label; })
                    .WithChildren(
                        new TreeItemWidget("Child")
                    )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent"), TimeSpan.FromSeconds(2), "tree to render")
            .Right()  // Expand
            .WaitUntil(s => s.ContainsText("Child"), TimeSpan.FromSeconds(2), "child to appear")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Parent", expandedLabel);
    }

    [Fact]
    public async Task Tree_OnCollapsed_FiresEvent()
    {
        var collapsedLabel = "";
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").Expanded()
                    .OnCollapsed(e => { collapsedLabel = e.Item.Label; })
                    .WithChildren(
                        new TreeItemWidget("Child")
                    )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("Child"), TimeSpan.FromSeconds(2), "tree to render expanded")
            .Left()  // Collapse
            .WaitUntil(s => !s.ContainsText("Child"), TimeSpan.FromSeconds(2), "child to disappear")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Parent", collapsedLabel);
    }

    #endregion

    #region Initial State Tests

    [Fact]
    public async Task Tree_FirstItemFocusedOnRender()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("First"),
                new TreeItemWidget("Second"),
                new TreeItemWidget("Third")
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First") && s.ContainsText("Second") && s.ContainsText("Third"), 
                TimeSpan.FromSeconds(2), "tree to render")
            .Capture("initial")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(IsFocused(snapshot, "First"), "First item should be initially focused");
    }

    [Fact]
    public async Task Tree_PreExpandedItems_ShowChildren()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => new TreeWidget([
                new TreeItemWidget("Root").Expanded().WithChildren(
                    new TreeItemWidget("Visible Child")
                )
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root") && s.ContainsText("Visible Child"), 
                TimeSpan.FromSeconds(2), "tree to render with expanded item")
            .Capture("initial")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Visible Child"), "Pre-expanded item should show children");
    }

    #endregion

    #region Border Rendering Investigation

    [Fact]
    public async Task Tree_BorderRendering_Investigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(100, 30)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.Text("🌳 Tree Widget Demo"),
                v.Separator(),
                
                v.HStack(h => [
                    h.Border(b => [
                        b.Tree(
                            new TreeItemWidget("Root").WithIcon("📁").Expanded().WithChildren(
                                new TreeItemWidget("Documents").WithIcon("📁").Expanded().WithChildren(
                                    new TreeItemWidget("report.docx").WithIcon("📄"),
                                    new TreeItemWidget("notes.txt").WithIcon("📄")
                                ),
                                new TreeItemWidget("Pictures").WithIcon("📸").Expanded().WithChildren(
                                    new TreeItemWidget("photo.jpg").WithIcon("📷")
                                )
                            )
                        ).FillHeight()
                    ], title: "📂 Files").FillWidth().FillHeight(),
                    
                    h.Border(b => [
                        b.Tree(
                            new TreeItemWidget("Options").Expanded().WithChildren(
                                new TreeItemWidget("Option A"),
                                new TreeItemWidget("Option B")
                            )
                        ).WithMultiSelect().FillHeight()
                    ], title: "✅ Select").FillWidth().FillHeight()
                ]).FillHeight()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Tree Widget Demo"), TimeSpan.FromSeconds(2), "demo to render")
            .Capture("border_investigation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify borders are present and correctly rendered
        Assert.True(snapshot.ContainsText("📂 Files"), "Left border title should be visible");
        Assert.True(snapshot.ContainsText("✅ Select"), "Right border title should be visible");
        
        // Verify that borders are correctly aligned at cell level
        // The two borders should be at columns 49 and 50 on line 7 (Pictures line)
        var leftBorderCell = snapshot.GetCell(49, 7);
        var rightBorderCell = snapshot.GetCell(50, 7);
        Assert.Equal("│", leftBorderCell.Character);
        Assert.Equal("│", rightBorderCell.Character);
    }

    #endregion
}
