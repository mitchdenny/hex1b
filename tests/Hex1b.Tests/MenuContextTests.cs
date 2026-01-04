using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class MenuContextTests
{
    [Fact]
    public void ParseAccelerator_NoAmpersand_ReturnsOriginalLabel()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("File");
        
        Assert.Equal("File", displayLabel);
        Assert.Null(accelerator);
        Assert.Equal(-1, index);
    }
    
    [Fact]
    public void ParseAccelerator_WithAmpersand_ExtractsAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("&File");
        
        Assert.Equal("File", displayLabel);
        Assert.Equal('F', accelerator);
        Assert.Equal(0, index);
    }
    
    [Fact]
    public void ParseAccelerator_AmpersandInMiddle_ExtractsAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Save &As");
        
        Assert.Equal("Save As", displayLabel);
        Assert.Equal('A', accelerator);
        Assert.Equal(5, index);
    }
    
    [Fact]
    public void ParseAccelerator_DoubleAmpersand_RendersLiteralAmpersand()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Tom && Jerry");
        
        Assert.Equal("Tom & Jerry", displayLabel);
        Assert.Null(accelerator);
        Assert.Equal(-1, index);
    }
    
    [Fact]
    public void ParseAccelerator_AmpersandAtEnd_NoAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Test&");
        
        Assert.Equal("Test", displayLabel);
        Assert.Null(accelerator);
        Assert.Equal(-1, index);
    }
    
    [Fact]
    public void ParseAccelerator_LowercaseLetter_ConvertsToUppercase()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("&open");
        
        Assert.Equal("open", displayLabel);
        Assert.Equal('O', accelerator);
        Assert.Equal(0, index);
    }
    
    [Fact]
    public void MenuItem_CreatedWithLabel()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open");
        
        Assert.Equal("Open", item.Label);
        Assert.False(item.IsDisabled);
        Assert.Null(item.ActivatedHandler);
    }
    
    [Fact]
    public void MenuItem_Disabled_SetsIsDisabled()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Undo").Disabled();
        
        Assert.True(item.IsDisabled);
    }
    
    [Fact]
    public void MenuItem_Disabled_CanBeToggled()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Undo").Disabled(true).Disabled(false);
        
        Assert.False(item.IsDisabled);
    }
    
    [Fact]
    public void MenuItem_OnActivated_SetsHandler()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open").OnActivated(_ => { });
        
        Assert.NotNull(item.ActivatedHandler);
    }
    
    [Fact]
    public void MenuItem_NoAccelerator_DisablesAutoAccelerator()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open").NoAccelerator();
        
        Assert.True(item.DisableAccelerator);
    }
    
    [Fact]
    public void Menu_CreatedWithChildren()
    {
        var ctx = new MenuContext();
        var menu = ctx.Menu("File", m => [
            m.MenuItem("New"),
            m.MenuItem("Open"),
            m.Separator(),
            m.MenuItem("Quit")
        ]);
        
        Assert.Equal("File", menu.Label);
        Assert.Equal(4, menu.Children.Count);
        Assert.IsType<MenuItemWidget>(menu.Children[0]);
        Assert.IsType<MenuItemWidget>(menu.Children[1]);
        Assert.IsType<MenuSeparatorWidget>(menu.Children[2]);
        Assert.IsType<MenuItemWidget>(menu.Children[3]);
    }
    
    [Fact]
    public void Menu_NestedSubmenus()
    {
        var ctx = new MenuContext();
        var menu = ctx.Menu("File", m => [
            m.MenuItem("New"),
            m.Menu("Recent", m => [
                m.MenuItem("doc1.txt"),
                m.MenuItem("doc2.txt")
            ])
        ]);
        
        Assert.Equal(2, menu.Children.Count);
        Assert.IsType<MenuItemWidget>(menu.Children[0]);
        
        var submenu = Assert.IsType<MenuWidget>(menu.Children[1]);
        Assert.Equal("Recent", submenu.Label);
        Assert.Equal(2, submenu.Children.Count);
    }
    
    [Fact]
    public void Separator_CreatedEmpty()
    {
        var ctx = new MenuContext();
        var separator = ctx.Separator();
        
        Assert.IsType<MenuSeparatorWidget>(separator);
    }
}
