using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for layout primitives: Constraints, Size, Rect.
/// </summary>
[TestClass]
public class LayoutTests
{
    #region Size Tests

    [TestMethod]
    public void Size_Constructor_SetsProperties()
    {
        var size = new Size(10, 20);
        
        Assert.AreEqual(10, size.Width);
        Assert.AreEqual(20, size.Height);
    }

    [TestMethod]
    public void Size_Zero_IsZero()
    {
        Assert.AreEqual(0, Size.Zero.Width);
        Assert.AreEqual(0, Size.Zero.Height);
    }

    [TestMethod]
    public void Size_Equality_Works()
    {
        var s1 = new Size(10, 20);
        var s2 = new Size(10, 20);
        var s3 = new Size(10, 30);
        
        Assert.AreEqual(s1, s2);
        Assert.AreNotEqual(s1, s3);
        Assert.IsTrue(s1 == s2);
        Assert.IsTrue(s1 != s3);
    }

    #endregion

    #region Rect Tests

    [TestMethod]
    public void Rect_Constructor_SetsProperties()
    {
        var rect = new Rect(5, 10, 100, 50);
        
        Assert.AreEqual(5, rect.X);
        Assert.AreEqual(10, rect.Y);
        Assert.AreEqual(100, rect.Width);
        Assert.AreEqual(50, rect.Height);
    }

    [TestMethod]
    public void Rect_Right_IsXPlusWidth()
    {
        var rect = new Rect(10, 0, 50, 20);
        
        Assert.AreEqual(60, rect.Right);
    }

    [TestMethod]
    public void Rect_Bottom_IsYPlusHeight()
    {
        var rect = new Rect(0, 10, 20, 30);
        
        Assert.AreEqual(40, rect.Bottom);
    }

    [TestMethod]
    public void Rect_Size_ReturnsCorrectSize()
    {
        var rect = new Rect(5, 5, 100, 50);
        
        Assert.AreEqual(new Size(100, 50), rect.Size);
    }

    [TestMethod]
    public void Rect_FromSize_CreatesRectAtOrigin()
    {
        var size = new Size(80, 24);
        var rect = Rect.FromSize(size);
        
        Assert.AreEqual(0, rect.X);
        Assert.AreEqual(0, rect.Y);
        Assert.AreEqual(80, rect.Width);
        Assert.AreEqual(24, rect.Height);
    }

    [TestMethod]
    public void Rect_Equality_Works()
    {
        var r1 = new Rect(1, 2, 3, 4);
        var r2 = new Rect(1, 2, 3, 4);
        var r3 = new Rect(1, 2, 3, 5);
        
        Assert.AreEqual(r1, r2);
        Assert.AreNotEqual(r1, r3);
    }

    #endregion

    #region Constraints Tests

    [TestMethod]
    public void Constraints_Constructor_SetsProperties()
    {
        var c = new Constraints(10, 100, 5, 50);
        
        Assert.AreEqual(10, c.MinWidth);
        Assert.AreEqual(100, c.MaxWidth);
        Assert.AreEqual(5, c.MinHeight);
        Assert.AreEqual(50, c.MaxHeight);
    }

    [TestMethod]
    public void Constraints_Unbounded_HasMaxValues()
    {
        var c = Constraints.Unbounded;
        
        Assert.AreEqual(0, c.MinWidth);
        Assert.AreEqual(int.MaxValue, c.MaxWidth);
        Assert.AreEqual(0, c.MinHeight);
        Assert.AreEqual(int.MaxValue, c.MaxHeight);
    }

    [TestMethod]
    public void Constraints_Tight_HasEqualMinMax()
    {
        var c = Constraints.Tight(80, 24);
        
        Assert.AreEqual(80, c.MinWidth);
        Assert.AreEqual(80, c.MaxWidth);
        Assert.AreEqual(24, c.MinHeight);
        Assert.AreEqual(24, c.MaxHeight);
    }

    [TestMethod]
    public void Constraints_Loose_HasZeroMin()
    {
        var c = Constraints.Loose(80, 24);
        
        Assert.AreEqual(0, c.MinWidth);
        Assert.AreEqual(80, c.MaxWidth);
        Assert.AreEqual(0, c.MinHeight);
        Assert.AreEqual(24, c.MaxHeight);
    }

    [TestMethod]
    public void Constraints_Constrain_ClampsToMin()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(5, 5);
        
        var result = c.Constrain(size);
        
        Assert.AreEqual(10, result.Width);
        Assert.AreEqual(10, result.Height);
    }

    [TestMethod]
    public void Constraints_Constrain_ClampsToMax()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(200, 200);
        
        var result = c.Constrain(size);
        
        Assert.AreEqual(100, result.Width);
        Assert.AreEqual(100, result.Height);
    }

    [TestMethod]
    public void Constraints_Constrain_PreservesWithinBounds()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(50, 50);
        
        var result = c.Constrain(size);
        
        Assert.AreEqual(50, result.Width);
        Assert.AreEqual(50, result.Height);
    }

    [TestMethod]
    public void Constraints_WithWidth_SetsFixedWidth()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithWidth(50);
        
        Assert.AreEqual(50, result.MinWidth);
        Assert.AreEqual(50, result.MaxWidth);
        Assert.AreEqual(0, result.MinHeight);
        Assert.AreEqual(100, result.MaxHeight);
    }

    [TestMethod]
    public void Constraints_WithHeight_SetsFixedHeight()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithHeight(50);
        
        Assert.AreEqual(0, result.MinWidth);
        Assert.AreEqual(100, result.MaxWidth);
        Assert.AreEqual(50, result.MinHeight);
        Assert.AreEqual(50, result.MaxHeight);
    }

    [TestMethod]
    public void Constraints_WithMaxWidth_ReducesMax()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithMaxWidth(50);
        
        Assert.AreEqual(0, result.MinWidth);
        Assert.AreEqual(50, result.MaxWidth);
    }

    [TestMethod]
    public void Constraints_WithMaxWidth_DoesNotIncrease()
    {
        var c = Constraints.Loose(50, 50);
        var result = c.WithMaxWidth(100);
        
        Assert.AreEqual(50, result.MaxWidth);
    }

    [TestMethod]
    public void Constraints_Equality_Works()
    {
        var c1 = new Constraints(1, 2, 3, 4);
        var c2 = new Constraints(1, 2, 3, 4);
        var c3 = new Constraints(1, 2, 3, 5);
        
        Assert.AreEqual(c1, c2);
        Assert.AreNotEqual(c1, c3);
    }

    #endregion
}
