using Hex1b.Widgets;

#pragma warning disable HEX1B001 // Experimental API

namespace Hex1b.Tests;

[TestClass]
public class NavigatorStateTests
{
    private static NavigatorRoute CreateRoute(string id) =>
        new(id, nav => new TextBlockWidget($"Screen: {id}"));

    [TestMethod]
    public void Constructor_InitializesWithRootRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        Assert.AreEqual("home", navigator.CurrentRoute.Id);
        Assert.AreEqual(1, navigator.Depth);
        Assert.IsFalse(navigator.CanGoBack);
    }

    [TestMethod]
    public void Push_AddsRouteToStack()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        navigator.Push(CreateRoute("details"));

        Assert.AreEqual("details", navigator.CurrentRoute.Id);
        Assert.AreEqual(2, navigator.Depth);
        Assert.IsTrue(navigator.CanGoBack);
    }

    [TestMethod]
    public void Push_WithIdAndBuilder_AddsRouteToStack()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        navigator.Push("details", nav => new TextBlockWidget("Details"));

        Assert.AreEqual("details", navigator.CurrentRoute.Id);
        Assert.AreEqual(2, navigator.Depth);
    }

    [TestMethod]
    public void Pop_RemovesTopRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("details"));
        navigator.Push(CreateRoute("edit"));

        var result = navigator.Pop();

        Assert.IsTrue(result);
        Assert.AreEqual("details", navigator.CurrentRoute.Id);
        Assert.AreEqual(2, navigator.Depth);
    }

    [TestMethod]
    public void Pop_AtRoot_ReturnsFalse()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        var result = navigator.Pop();

        Assert.IsFalse(result);
        Assert.AreEqual("home", navigator.CurrentRoute.Id);
        Assert.AreEqual(1, navigator.Depth);
    }

    [TestMethod]
    public void PopToRoot_ClearsAllButRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));
        navigator.Push(CreateRoute("step3"));

        navigator.PopToRoot();

        Assert.AreEqual("home", navigator.CurrentRoute.Id);
        Assert.AreEqual(1, navigator.Depth);
        Assert.IsFalse(navigator.CanGoBack);
    }

    [TestMethod]
    public void Replace_SwapsCurrentRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));

        navigator.Replace(CreateRoute("redirect"));

        Assert.AreEqual("redirect", navigator.CurrentRoute.Id);
        Assert.AreEqual(2, navigator.Depth); // Still 2 - replaced, not added
    }

    [TestMethod]
    public void Replace_WithIdAndBuilder_SwapsCurrentRoute()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));

        navigator.Replace("redirect", nav => new TextBlockWidget("Redirect"));

        Assert.AreEqual("redirect", navigator.CurrentRoute.Id);
        Assert.AreEqual(2, navigator.Depth);
    }

    [TestMethod]
    public void Reset_ClearsStackAndSetsNewRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));

        navigator.Reset(CreateRoute("new-home"));

        Assert.AreEqual("new-home", navigator.CurrentRoute.Id);
        Assert.AreEqual(1, navigator.Depth);
        Assert.IsFalse(navigator.CanGoBack);
    }

    [TestMethod]
    public void OnNavigated_FiresOnPush()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.Push(CreateRoute("details"));

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void OnNavigated_FiresOnPop()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("details"));
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.Pop();

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void OnNavigated_FiresOnPopToRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);
        navigator.Push(CreateRoute("step1"));
        navigator.Push(CreateRoute("step2"));
        var eventFired = false;
        navigator.OnNavigated += () => eventFired = true;

        navigator.PopToRoot();

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void WizardFlow_PushThenPopToRoot()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        // Simulate a 3-step wizard
        navigator.Push(CreateRoute("wizard-step-1"));
        navigator.Push(CreateRoute("wizard-step-2"));
        navigator.Push(CreateRoute("wizard-step-3"));
        Assert.AreEqual(4, navigator.Depth);

        // Complete wizard - go back to home
        navigator.PopToRoot();
        Assert.AreEqual("home", navigator.CurrentRoute.Id);
        Assert.IsFalse(navigator.CanGoBack);
    }

    [TestMethod]
    public void DrillDown_CanNavigateBackStepByStep()
    {
        var root = CreateRoute("home");
        var navigator = new NavigatorState(root);

        // Drill down
        navigator.Push(CreateRoute("category"));
        navigator.Push(CreateRoute("item"));
        navigator.Push(CreateRoute("details"));

        // Navigate back step by step
        Assert.IsTrue(navigator.Pop());
        Assert.AreEqual("item", navigator.CurrentRoute.Id);

        Assert.IsTrue(navigator.Pop());
        Assert.AreEqual("category", navigator.CurrentRoute.Id);

        Assert.IsTrue(navigator.Pop());
        Assert.AreEqual("home", navigator.CurrentRoute.Id);

        Assert.IsFalse(navigator.Pop()); // Can't go back further
    }
}
