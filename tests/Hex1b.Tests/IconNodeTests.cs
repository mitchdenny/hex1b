using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class IconNodeTests
{
    [TestMethod]
    public void Measure_SingleCharIcon_Returns1x1()
    {
        var node = new IconNode { Icon = "▶" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        
        Assert.AreEqual(1, size.Width);
        Assert.AreEqual(1, size.Height);
    }
    
    [TestMethod]
    public void Measure_MultiCharIcon_ReturnsCorrectWidth()
    {
        var node = new IconNode { Icon = "[x]" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        
        Assert.AreEqual(3, size.Width);
        Assert.AreEqual(1, size.Height);
    }
    
    [TestMethod]
    public void IsClickable_WithoutHandler_ReturnsFalse()
    {
        var node = new IconNode { Icon = "▶" };
        Assert.IsFalse(node.IsClickable);
    }
    
    [TestMethod]
    public void IsClickable_WithHandler_ReturnsTrue()
    {
        var node = new IconNode 
        { 
            Icon = "▶",
            ClickCallback = _ => Task.CompletedTask
        };
        Assert.IsTrue(node.IsClickable);
    }
    
    [TestMethod]
    public void ConfigureDefaultBindings_WithHandler_AddsMouseBinding()
    {
        var node = new IconNode 
        { 
            Icon = "▶",
            ClickCallback = _ => Task.CompletedTask
        };
        
        var builder = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(builder);
        
        Assert.IsNotEmpty(builder.MouseBindings);
    }
    
    [TestMethod]
    public void ConfigureDefaultBindings_WithoutHandler_NoBindings()
    {
        var node = new IconNode { Icon = "▶" };
        
        var builder = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(builder);
        
        Assert.IsEmpty(builder.MouseBindings);
    }
    
    [TestMethod]
    public async Task Reconcile_CreatesNewNode()
    {
        var widget = new IconWidget("▶");
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(null, context);
        
        TestSeq.IsType<IconNode>(node);
        Assert.AreEqual("▶", ((IconNode)node).Icon);
    }
    
    [TestMethod]
    public async Task Reconcile_UpdatesExistingNode()
    {
        var existingNode = new IconNode { Icon = "▶" };
        var widget = new IconWidget("▼");
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(existingNode, context);
        
        Assert.AreSame(existingNode, node);
        Assert.AreEqual("▼", ((IconNode)node).Icon);
    }
    
    [TestMethod]
    public async Task Reconcile_WithClickHandler_SetsCallback()
    {
        var widget = new IconWidget("▶").OnClick(_ => { });
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(null, context);
        var iconNode = (IconNode)node;
        
        Assert.IsNotNull(iconNode.ClickCallback);
    }
    
    [TestMethod]
    public void Widget_OnClick_ReturnsNewWidgetWithHandler()
    {
        var widget = new IconWidget("▶");
        var withClick = widget.OnClick(_ => { });
        
        Assert.AreNotSame(widget, withClick);
        Assert.IsNotNull(withClick.ClickHandler);
    }
}
