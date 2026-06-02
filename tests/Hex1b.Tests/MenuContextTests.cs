using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class MenuContextTests
{
    [TestMethod]
    public void ParseAccelerator_NoAmpersand_ReturnsOriginalLabel()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("File");
        
        Assert.AreEqual("File", displayLabel);
        Assert.IsNull(accelerator);
        Assert.AreEqual(-1, index);
    }
    
    [TestMethod]
    public void ParseAccelerator_WithAmpersand_ExtractsAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("&File");
        
        Assert.AreEqual("File", displayLabel);
        Assert.AreEqual('F', accelerator);
        Assert.AreEqual(0, index);
    }
    
    [TestMethod]
    public void ParseAccelerator_AmpersandInMiddle_ExtractsAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Save &As");
        
        Assert.AreEqual("Save As", displayLabel);
        Assert.AreEqual('A', accelerator);
        Assert.AreEqual(5, index);
    }
    
    [TestMethod]
    public void ParseAccelerator_DoubleAmpersand_RendersLiteralAmpersand()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Tom && Jerry");
        
        Assert.AreEqual("Tom & Jerry", displayLabel);
        Assert.IsNull(accelerator);
        Assert.AreEqual(-1, index);
    }
    
    [TestMethod]
    public void ParseAccelerator_AmpersandAtEnd_NoAccelerator()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("Test&");
        
        Assert.AreEqual("Test", displayLabel);
        Assert.IsNull(accelerator);
        Assert.AreEqual(-1, index);
    }
    
    [TestMethod]
    public void ParseAccelerator_LowercaseLetter_ConvertsToUppercase()
    {
        var (displayLabel, accelerator, index) = MenuContext.ParseAccelerator("&open");
        
        Assert.AreEqual("open", displayLabel);
        Assert.AreEqual('O', accelerator);
        Assert.AreEqual(0, index);
    }
    
    [TestMethod]
    public void MenuItem_CreatedWithLabel()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open");
        
        Assert.AreEqual("Open", item.Label);
        Assert.IsFalse(item.IsDisabled);
        Assert.IsNull(item.ActivatedHandler);
    }
    
    [TestMethod]
    public void MenuItem_Disabled_SetsIsDisabled()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Undo").Disabled();
        
        Assert.IsTrue(item.IsDisabled);
    }
    
    [TestMethod]
    public void MenuItem_Disabled_CanBeToggled()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Undo").Disabled(true).Disabled(false);
        
        Assert.IsFalse(item.IsDisabled);
    }
    
    [TestMethod]
    public void MenuItem_OnActivated_SetsHandler()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open").OnActivated(_ => { });
        
        Assert.IsNotNull(item.ActivatedHandler);
    }
    
    [TestMethod]
    public void MenuItem_NoAccelerator_DisablesAutoAccelerator()
    {
        var ctx = new MenuContext();
        var item = ctx.MenuItem("Open").NoAccelerator();
        
        Assert.IsTrue(item.DisableAccelerator);
    }
    
    [TestMethod]
    public void Menu_CreatedWithChildren()
    {
        var ctx = new MenuContext();
        var menu = ctx.Menu("File", m => [
            m.MenuItem("New"),
            m.MenuItem("Open"),
            m.Separator(),
            m.MenuItem("Quit")
        ]);
        
        Assert.AreEqual("File", menu.Label);
        Assert.AreEqual(4, menu.Children.Count);
        TestSeq.IsType<MenuItemWidget>(menu.Children[0]);
        TestSeq.IsType<MenuItemWidget>(menu.Children[1]);
        TestSeq.IsType<MenuSeparatorWidget>(menu.Children[2]);
        TestSeq.IsType<MenuItemWidget>(menu.Children[3]);
    }
    
    [TestMethod]
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
        
        Assert.AreEqual(2, menu.Children.Count);
        TestSeq.IsType<MenuItemWidget>(menu.Children[0]);
        
        var submenu = TestSeq.IsType<MenuWidget>(menu.Children[1]);
        Assert.AreEqual("Recent", submenu.Label);
        Assert.AreEqual(2, submenu.Children.Count);
    }
    
    [TestMethod]
    public void Separator_CreatedEmpty()
    {
        var ctx = new MenuContext();
        var separator = ctx.Separator();
        
        TestSeq.IsType<MenuSeparatorWidget>(separator);
    }
}
