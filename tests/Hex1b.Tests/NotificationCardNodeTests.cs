using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for NotificationCardNode rendering and behavior.
/// </summary>
public class NotificationCardNodeTests
{
    [Fact]
    public void NotificationCardNode_Measure_ReturnsReasonableSize()
    {
        var notification = new Notification("Test Title", "Test body text");
        var node = new NotificationCardNode
        {
            Notification = notification,
            Title = notification.Title,
            Body = notification.Body
        };

        var size = node.Measure(Constraints.Unbounded);

        // Should have non-zero dimensions
        Assert.True(size.Width > 0, "Width should be positive");
        Assert.True(size.Height > 0, "Height should be positive");
    }

    [Fact]
    public async Task NotificationCardNode_WithAction_CreatesChildrenViaReconciliation()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View Details", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget to create children
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        // Verify children were created
        Assert.NotNull(node.DismissButton);
        Assert.NotNull(node.ActionButton);
        Assert.Equal("View Details", node.ActionButton.PrimaryLabel);
    }

    [Fact]
    public async Task NotificationCardNode_WithAction_MeasuresWithChildren()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        var size = node.Measure(Constraints.Unbounded);

        // With title + body + action + progress, should have reasonable height
        Assert.True(size.Height >= 3, $"Height should be at least 3, was {size.Height}");
    }

    [Fact]
    public async Task NotificationCardNode_Children_ContainsButtonsAfterReconciliation()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        // GetChildren should return the button nodes
        var children = node.GetChildren().ToList();
        Assert.NotEmpty(children);
        Assert.Contains(children, c => c is ButtonNode);
        Assert.Contains(children, c => c is SplitButtonNode);
    }

    [Fact]
    public async Task NotificationCardNode_DismissButton_AlwaysCreated()
    {
        // Even without primary action, dismiss button should exist
        var notification = new Notification("Title", "Body");
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        Assert.NotNull(node.DismissButton);
        Assert.Null(node.ActionButton); // No action button without primary action
    }
}
