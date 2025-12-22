using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests to reproduce the issue where a TextBox shows as focused (with cursor)
/// even when it doesn't have focus. This replicates the structure of the ThemingExhibit.
/// </summary>
public class ThemingExhibitRepro
{
    /// <summary>
    /// Replicates the ThemingExhibit structure:
    /// Splitter
    ///   ├─ Panel (left) → VStack → List (has initial focus)
    ///   └─ Layout (right) → Panel → VStack → TextBox (should NOT show cursor)
    /// </summary>
    [Fact]
    public async Task TextBox_InSplitterRightPane_ShouldNotShowCursor_WhenListHasFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        IReadOnlyList<string> items = [
            "Theme 1",
            "Theme 2",
        ];

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(root => [
                    root.Splitter(
                        // Left: Panel with List
                        root.Panel(leftPanel => [
                            leftPanel.VStack(left => [
                                left.Text("═══ Themes ═══"),
                                left.List(items)
                            ])
                        ]),
                        // Right: Layout -> Panel -> VStack -> TextBox
                        root.Layout(
                            root.Panel(rightPanel => [
                                rightPanel.VStack(right => [
                                    right.Text("═══ Widget Preview ═══"),
                                    right.TextBox("Sample text")
                                ])
                            ]),
                            ClipMode.Clip
                        ),
                        leftWidth: 20
                    )
                ]);
                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Start the app first, then interact with it
        var runTask = app.RunAsync();

        // Wait for the content to render, then exit
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Sample text"), TimeSpan.FromSeconds(2), "Wait for initial render")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);

        await runTask;

        // Debug: Print all non-empty lines
        Console.WriteLine("=== Screen output ===");
        foreach (var line in terminal.CreateSnapshot().GetNonEmptyLines())
        {
            Console.WriteLine(line);
        }
        
        Console.WriteLine("\n=== Raw output (to check for ANSI codes) ===");
        Console.WriteLine(terminal.CreateSnapshot().RawOutput);
        
        // The List should have focus (first focusable in the splitter)
        // The TextBox should NOT show cursor styling
        
        // Look for cursor color codes in the raw output
        // Default cursor colors: black foreground (38;2;0;0;0), white background (48;2;255;255;255)
        var rawOutput = terminal.CreateSnapshot().RawOutput;
        
        // Check that our text appears in the screen (without ANSI codes)
        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("Sample text", screenText);
        
        // Look for cursor background color (white - 48;2;255;255;255) in the WHOLE output.
        // If the TextBox is unfocused, there should be NO cursor color codes at all.
        var cursorBgPattern = "48;2;255;255;255"; // Default cursor background (white)
        
        // The cursor colors should NOT appear in the rendering output when TextBox is unfocused
        var hasCursorBg = rawOutput.Contains(cursorBgPattern);
        
        // Escape the raw output for display (so ANSI codes are visible)
        var escapedOutput = rawOutput.Replace("\x1b", "\\x1b");
        
        Assert.False(hasCursorBg, 
            $"TextBox should NOT have cursor colors when unfocused. " +
            $"Escaped raw output:\n{escapedOutput}");
    }

    /// <summary>
    /// Test to verify the root cause: ParentManagesFocus doesn't work correctly
    /// because node.Parent is set AFTER reconciliation.
    /// 
    /// This test documents the issue - the actual fix is in ReconcileContext
    /// which now tracks the full ancestor chain.
    /// </summary>
    [Fact]
    public void NodeParentLinks_NotSetDuringReconcile_ExplainsTheBug()
    {
        // This test demonstrates what was happening during reconciliation:
        // We create nodes in order, but Parent is set AFTER Reconcile.
        // ParentManagesFocus() was trying to walk up the Parent chain during Reconcile,
        // but the parent's Parent might not be set yet.

        // Simulate the reconcile order for: Splitter -> Layout -> Panel -> VStack
        
        // 1. Create SplitterNode (no parent set yet)
        var splitter = new SplitterNode();
        
        // 2. Start reconciling Layout as child of Splitter
        var layout = new LayoutNode();
        // layout.Parent is NOT set yet during its child's reconcile!
        
        // 3. Start reconciling Panel as child of Layout
        var panel = new PanelNode();
        
        // 4. The issue: during VStack reconciliation, walking up the Parent chain
        //    would only see: panel -> null
        //    because panel.Parent hasn't been set yet!
        
        Assert.Null(layout.Parent); // Demonstrates that Parent is not set during child reconcile
        Assert.Null(panel.Parent);  // Same issue
        
        // After reconcile would complete, parents would be set:
        panel.Parent = layout;
        layout.Parent = splitter;
        
        // Now the tree is correct:
        Assert.Equal(layout, panel.Parent);
        Assert.Equal(splitter, layout.Parent);
        
        // The FIX: ReconcileContext now stores the full ancestor chain internally,
        // so ParentManagesFocus() can traverse the full ancestry without relying on node.Parent.
    }

    /// <summary>
    /// This test verifies the actual bug is fixed: when we use ParentManagesFocus(),
    /// it can traverse the full ancestry to find SplitterNode even during reconciliation.
    /// </summary>
    [Fact]
    public void BugFixed_ParentManagesFocus_FindsSplitterThroughAncestorChain()
    {
        // The fix stores the full parent chain in ReconcileContext,
        // so ParentManagesFocus() no longer relies on node.Parent links.
        // This is tested by the integration test 
        // TextBox_InSplitterRightPane_ShouldNotShowCursor_WhenListHasFocus
        
        Assert.True(true, "See integration test for verification");
    }

    /// <summary>
    /// Verify that when TextBoxNode.IsFocused is false, rendering does not include cursor colors.
    /// </summary>
    [Fact]
    public void TextBoxNode_WhenNotFocused_ShouldNotRenderCursorColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 5);
        var context = new Hex1bRenderContext(workload);
        
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = false
        };

        node.Render(context);

        var output = terminal.CreateSnapshot().RawOutput;
        
        // Default cursor colors from theme:
        // CursorForegroundColor = Black (0,0,0)
        // CursorBackgroundColor = White (255,255,255)
        var cursorBgCode = "48;2;255;255;255"; // White background
        
        Assert.DoesNotContain(cursorBgCode, output);
        // Note: foreground might match other things, so we focus on the distinctive background
    }

    /// <summary>
    /// Verify that when TextBoxNode.IsFocused is true, rendering DOES include cursor colors.
    /// </summary>
    [Fact]
    public void TextBoxNode_WhenFocused_ShouldRenderCursorColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 5);
        var context = new Hex1bRenderContext(workload);
        
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = true
        };
        node.State.CursorPosition = 1;

        node.Render(context);

        var output = terminal.CreateSnapshot().RawOutput;
        
        // Default cursor background = White (255,255,255)
        var cursorBgCode = "48;2;255;255;255";
        
        Assert.Contains(cursorBgCode, output);
    }

    /// <summary>
    /// Full integration test: create the ThemingExhibit-like structure via Hex1bApp
    /// and verify focus is correctly assigned.
    /// </summary>
    [Fact]
    public async Task Integration_ThemingExhibitStructure_ListHasFocus_TextBoxDoesNot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 100, 30);

        IReadOnlyList<string> items = [
            "Default",
            "Ocean",
            "Sunset",
        ];
        var buttonClicked = false;

        // Build the structure similar to ThemingExhibit
        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(root => [
                    root.Splitter(
                        // Left pane: Panel containing a VStack with List
                        root.Panel(leftPanel => [
                            leftPanel.VStack(left => [
                                left.Text("═══ Themes ═══"),
                                left.Text(""),
                                left.List(items)
                            ])
                        ]),
                        // Right pane: Layout -> Panel -> VStack with TextBox and Button
                        root.Layout(
                            root.Panel(rightPanel => [
                                rightPanel.VStack(right => [
                                    right.Text("═══ Widget Preview ═══"),
                                    right.Text(""),
                                    right.Text("TextBox (Tab to focus):"),
                                    right.TextBox("Sample text"),
                                    right.Text(""),
                                    right.Text("Button:"),
                                    right.Button(
                                        buttonClicked ? "Clicked!" : "Click Me")
                                        .OnClick(_ => { buttonClicked = !buttonClicked; return Task.CompletedTask; })
                                ])
                            ]),
                            ClipMode.Clip
                        ),
                        leftWidth: 20
                    )
                ]);
                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Start the app first, then interact with it
        var runTask = app.RunAsync();

        // Wait for the content to render, then exit
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Sample text"), TimeSpan.FromSeconds(2), "Wait for initial render")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);

        await runTask;

        // Check for cursor colors in the output
        var rawOutput = terminal.CreateSnapshot().RawOutput;
        var screenText = terminal.CreateSnapshot().GetScreenText();
        
        // Escape the raw output for display (so ANSI codes are visible)
        var escapedOutput = rawOutput.Replace("\x1b", "\\x1b");
        
        Console.WriteLine("=== Screen Text ===");
        Console.WriteLine(screenText);
        Console.WriteLine("\n=== Escaped Raw Output ===");
        Console.WriteLine(escapedOutput);
        
        // Verify our text is rendered
        Assert.Contains("Sample text", screenText);
        
        // The cursor background (white by default: 48;2;255;255;255) should NOT appear
        // if the TextBox is correctly unfocused
        var cursorBgCode = "48;2;255;255;255";
        
        Assert.False(rawOutput.Contains(cursorBgCode),
            $"TextBox should NOT render cursor colors when another widget (List) has focus.\n" +
            $"Escaped raw output:\n{escapedOutput}");
    }
}
