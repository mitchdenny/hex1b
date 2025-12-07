using Hex1b.Widgets;

#pragma warning disable HEX1B001 // Experimental API

namespace Hex1b.Tests;

public class NavigatorStateTests
{
    private static NavigatorRoute CreateRoute(string id) =>
        new(id, nav => new TextBlockWidget($"Screen: {id}"));

    [Fact]
    public void Constructor_InitializesWithRootRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        Assert.Equal("home", navigator.CurrentRoute.Id);
        Assert.Equal(1, navigator.Depth);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void Push_AddsRouteToStack()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        navigator.Push(CreateRoute("details"));

        Assert.Equal("details", navigator.CurrentRoute.Id);
        Assert.Equal(2, navigator.Depth);
        Assert.True(navigator.CanGoBack);
    }

    [Fact]
    public void Push_WithIdAndBuilder_AddsRouteToStack()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        navigator.Push("details", nav => new TextBlockWidget("Details"));

        Assert.Equal("details", navigator.CurrentRoute.Id);
        Assert.Equal(2, navigator.Depth);
    }

    [Fact]
    public void Pop_RemovesTopRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("details"));
        navigator.Push(CreateRoute("edit"));

        var result = navigator.Pop();

        Assert.True(result);
        Assert.Equal("details", navigator.CurrentRoute.Id);
        Assert.Equal(2, navigator.Depth);
    }

    [Fact]
    public void Pop_AtRoot_ReturnsFalse()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        var result = navigator.Pop();

        Assert.False(result);
        Assert.Equal("home", navigator.CurrentRoute.Id);
        Assert.Equal(1, navigator.Depth);
    }

    [Fact]
    public void PopToRoot_ClearsAllButRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));
        navigator.Push(CreateRoute("step3"));

        navigator.PopToRoot();

        Assert.Equal("home", navigator.CurrentRoute.Id);
        Assert.Equal(1, navigator.Depth);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void Replace_SwapsCurrentRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));

        navigator.Replace(CreateRoute("redirect"));

        Assert.Equal("redirect", navigator.CurrentRoute.Id);
        Assert.Equal(2, navigator.Depth); // Still 2 - replaced, not added
    }

    [Fact]
    public void Replace_WithIdAndBuilder_SwapsCurrentRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));

        navigator.Replace("redirect", nav => new TextBlockWidget("Redirect"));

        Assert.Equal("redirect", navigator.CurrentRoute.Id);
        Assert.Equal(2, navigator.Depth);
    }

    [Fact]
    public void Reset_ClearsStackAndSetsNewRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));

        navigator.Reset(CreateRoute("new-home"));

        Assert.Equal("new-home", navigator.CurrentRoute.Id);
        Assert.Equal(1, navigator.Depth);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void OnNavigated_FiresOnPush()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.Push(CreateRoute("details"));

        Assert.True(eventFired);
    }

    [Fact]
    public void OnNavigated_FiresOnPop()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("details"));
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.Pop();

        Assert.True(eventFired);
    }

    [Fact]
    public void OnNavigated_FiresOnPopToRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.PopToRoot();

        Assert.True(eventFired);
    }

    [Fact]
    public void WizardFlow_PushThenPopToRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        // Simulate a 3-step wizard
        navigator.Push(CreateRoute("wizard-step-1"));
        navigator.Push(CreateRoute("wizard-step-2"));
        navigator.Push(CreateRoute("wizard-step-3"));
        Assert.Equal(4, navigator.Depth);

        // Complete wizard - go back to home
        navigator.PopToRoot();
        Assert.Equal("home", navigator.CurrentRoute.Id);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void DrillDown_CanNavigateBackStepByStep()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        // Drill down
        navigator.Push(CreateRoute("category"));
        navigator.Push(CreateRoute("item"));
        navigator.Push(CreateRoute("details"));

        // Navigate back step by step
        Assert.True(navigator.Pop());
        Assert.Equal("item", navigator.CurrentRoute.Id);

        Assert.True(navigator.Pop());
        Assert.Equal("category", navigator.CurrentRoute.Id);

        Assert.True(navigator.Pop());
        Assert.Equal("home", navigator.CurrentRoute.Id);

        Assert.False(navigator.Pop()); // Can't go back further
    }
}
