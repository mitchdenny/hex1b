using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class IconNodeTests
{
    [Fact]
    public void Measure_SingleCharIcon_Returns1x1()
    {
        var node = new IconNode { Icon = "▶" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        
        Assert.Equal(1, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void Measure_MultiCharIcon_ReturnsCorrectWidth()
    {
        var node = new IconNode { Icon = "[x]" };
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        
        Assert.Equal(3, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void Measure_WhenLoading_ReturnsSpinnerFrameWidth()
    {
        var node = new IconNode { Icon = "▶", IsLoading = true };
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        
        // Default spinner (Dots) has single-char frames
        Assert.True(size.Width >= 1);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void IsClickable_WithoutHandler_ReturnsFalse()
    {
        var node = new IconNode { Icon = "▶" };
        Assert.False(node.IsClickable);
    }
    
    [Fact]
    public void IsClickable_WithHandler_ReturnsTrue()
    {
        var node = new IconNode 
        { 
            Icon = "▶",
            ClickCallback = _ => Task.CompletedTask
        };
        Assert.True(node.IsClickable);
    }
    
    [Fact]
    public void ConfigureDefaultBindings_WithHandler_AddsMouseBinding()
    {
        var node = new IconNode 
        { 
            Icon = "▶",
            ClickCallback = _ => Task.CompletedTask
        };
        
        var builder = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(builder);
        
        Assert.NotEmpty(builder.MouseBindings);
    }
    
    [Fact]
    public void ConfigureDefaultBindings_WithoutHandler_NoBindings()
    {
        var node = new IconNode { Icon = "▶" };
        
        var builder = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(builder);
        
        Assert.Empty(builder.MouseBindings);
    }
    
    [Fact]
    public void ResetLoadingAnimation_ResetsTimer()
    {
        var node = new IconNode { Icon = "▶", IsLoading = true };
        
        // Just verify it doesn't throw
        node.ResetLoadingAnimation();
    }
    
    [Fact]
    public async Task Reconcile_CreatesNewNode()
    {
        var widget = new IconWidget("▶");
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(null, context);
        
        Assert.IsType<IconNode>(node);
        Assert.Equal("▶", ((IconNode)node).Icon);
    }
    
    [Fact]
    public async Task Reconcile_UpdatesExistingNode()
    {
        var existingNode = new IconNode { Icon = "▶" };
        var widget = new IconWidget("▼");
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(existingNode, context);
        
        Assert.Same(existingNode, node);
        Assert.Equal("▼", ((IconNode)node).Icon);
    }
    
    [Fact]
    public async Task Reconcile_WithLoading_SetsLoadingState()
    {
        var widget = new IconWidget("▶").WithLoading(true);
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(null, context);
        
        Assert.True(((IconNode)node).IsLoading);
    }
    
    [Fact]
    public async Task Reconcile_WithClickHandler_SetsCallback()
    {
        var widget = new IconWidget("▶").OnClick(_ => { });
        var context = ReconcileContext.CreateRoot();
        
        var node = await widget.ReconcileAsync(null, context);
        var iconNode = (IconNode)node;
        
        Assert.NotNull(iconNode.ClickCallback);
    }
    
    [Fact]
    public void Widget_OnClick_ReturnsNewWidgetWithHandler()
    {
        var widget = new IconWidget("▶");
        var withClick = widget.OnClick(_ => { });
        
        Assert.NotSame(widget, withClick);
        Assert.NotNull(withClick.ClickHandler);
    }
    
    [Fact]
    public void Widget_WithLoading_ReturnsNewWidget()
    {
        var widget = new IconWidget("▶");
        var loading = widget.WithLoading(true);
        
        Assert.NotSame(widget, loading);
        Assert.True(loading.IsLoading);
        Assert.False(widget.IsLoading); // Original unchanged
    }
    
    [Fact]
    public void Widget_WithLoadingStyle_SetsStyle()
    {
        var widget = new IconWidget("▶");
        var withStyle = widget.WithLoadingStyle(SpinnerStyle.Arrow);
        
        Assert.Equal(SpinnerStyle.Arrow, withStyle.LoadingStyle);
    }
    
    [Fact]
    public void GetEffectiveRedrawDelay_NotLoading_ReturnsNull()
    {
        var widget = new IconWidget("▶");
        Assert.Null(widget.GetEffectiveRedrawDelay());
    }
    
    [Fact]
    public void GetEffectiveRedrawDelay_Loading_ReturnsSpinnerInterval()
    {
        var widget = new IconWidget("▶").WithLoading(true);
        var delay = widget.GetEffectiveRedrawDelay();
        
        Assert.NotNull(delay);
        Assert.Equal(SpinnerStyle.Dots.Interval, delay);
    }
}
