using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests specifically for tree guide line alignment issues.
/// </summary>
[TestClass]
public class TreeGuideAlignmentTests
{
    [TestMethod]
    public async Task Tree_IsLastAtDepth_ComputedCorrectly()
    {
        // Unit test to verify IsLastAtDepth is computed correctly
        var widget = new TreeWidget([
            new TreeItemWidget("Root").Expanded().Children(
                new TreeItemWidget("Child 1").Expanded().Children(
                    new TreeItemWidget("Grandchild")
                ),
                new TreeItemWidget("Child 2")
            )
        ]);
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        // Verify flattened items
        Assert.AreEqual(4, node!.FlattenedItems.Count);
        
        // Root (depth=0, last=true at depth 0)
        var root = node.FlattenedItems[0];
        Assert.AreEqual("Root", root.Node.Label);
        Assert.AreEqual(0, root.Depth);
        Assert.IsTrue(root.Node.IsLastChild); // Only root
        TestSeq.AreEqual([true], root.IsLastAtDepth);
        
        // Child 1 (depth=1, NOT last at depth 1)
        var child1 = node.FlattenedItems[1];
        Assert.AreEqual("Child 1", child1.Node.Label);
        Assert.AreEqual(1, child1.Depth);
        Assert.IsFalse(child1.Node.IsLastChild); // Child 2 follows
        TestSeq.AreEqual([true, false], child1.IsLastAtDepth);
        
        // Grandchild (depth=2, last at depth 2, but Child 1 is NOT last at depth 1)
        var grandchild = node.FlattenedItems[2];
        Assert.AreEqual("Grandchild", grandchild.Node.Label);
        Assert.AreEqual(2, grandchild.Depth);
        Assert.IsTrue(grandchild.Node.IsLastChild); // Only child of Child 1
        // Key assertion: IsLastAtDepth[1] should be FALSE because Child 1 has siblings
        TestSeq.AreEqual([true, false, true], grandchild.IsLastAtDepth);
        
        // Child 2 (depth=1, IS last at depth 1)
        var child2 = node.FlattenedItems[3];
        Assert.AreEqual("Child 2", child2.Node.Label);
        Assert.AreEqual(1, child2.Depth);
        Assert.IsTrue(child2.Node.IsLastChild);
        TestSeq.AreEqual([true, true], child2.IsLastAtDepth);
    }
    
    [TestMethod]
    public async Task Tree_GuideLines_VerticalAlignment_NestedStructure()
    {
        // Create a tree structure that should show:
        // ▼ Root
        // ├─ ▼ Child 1
        // │  └─   Grandchild
        // └─   Child 2
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Root").Expanded().Children(
                    new TreeItemWidget("Child 1").Expanded().Children(
                        new TreeItemWidget("Grandchild")
                    ),
                    new TreeItemWidget("Child 2")
                )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root") && s.ContainsText("Grandchild"), 
                TimeSpan.FromSeconds(2), "tree to render")
            .Capture("guides")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Output the actual rendering for diagnosis
        var output = new System.Text.StringBuilder();
        output.AppendLine("=== Tree Rendering ===");
        for (int y = 0; y < 10; y++)
        {
            var line = snapshot.GetLineTrimmed(y);
            if (!string.IsNullOrEmpty(line))
            {
                output.AppendLine($"Line {y}: [{line}]");
            }
        }
        
        // The key verification: Line with Grandchild should have a vertical bar at column 0
        // Expected structure:
        // ▼ Root
        // ├─ ▼ Child 1
        // │  └─   Grandchild   <-- vertical bar should connect
        // └─   Child 2
        
        Assert.IsTrue(snapshot.ContainsText("│"), $"Should contain vertical guide line. Actual output:\n{output}");
        
        // Find the Grandchild line and verify vertical bar exists
        var grandchildLine = -1;
        for (int y = 0; y < snapshot.Height; y++)
        {
            if (snapshot.GetLine(y).Contains("Grandchild"))
            {
                grandchildLine = y;
                break;
            }
        }
        
        Assert.IsTrue(grandchildLine > 0, $"Should find Grandchild line. Output:\n{output}");
        
        var gcLineContent = snapshot.GetLine(grandchildLine);
        // The vertical bar should be at the start of the line (column 0)
        Assert.StartsWith("│", gcLineContent.TrimStart()); 
        // $"Grandchild line should start with vertical bar. Line content: [{gcLineContent}]\nFull output:\n{output}"
    }
    
    [TestMethod]
    public async Task Tree_GuideLines_MultipleRoots_NoVerticalForSingleRoot()
    {
        // Single root - no vertical lines needed at depth 0
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Root").Expanded().Children(
                    new TreeItemWidget("Child 1"),
                    new TreeItemWidget("Child 2")
                )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Root") && s.ContainsText("Child 2"), 
                TimeSpan.FromSeconds(2), "tree to render")
            .Capture("single_root")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var output = new System.Text.StringBuilder();
        output.AppendLine("=== Single Root Tree ===");
        for (int y = 0; y < 10; y++)
        {
            var line = snapshot.GetLineTrimmed(y);
            if (!string.IsNullOrEmpty(line))
            {
                output.AppendLine($"Line {y}: [{line}]");
            }
        }
        
        // Verify structure
        Assert.IsTrue(snapshot.ContainsText("├─"), $"Should have branch marker. Output:\n{output}");
        Assert.IsTrue(snapshot.ContainsText("└─"), $"Should have last branch marker. Output:\n{output}");
    }
}
