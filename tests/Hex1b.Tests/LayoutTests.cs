using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for layout primitives: Constraints, Size, Rect.
/// </summary>
public class LayoutTests
{
    #region Size Tests

    [Fact]
    public void Size_Constructor_SetsProperties()
    {
        var size = new Size(10, 20);
        
        Assert.Equal(10, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Size_Zero_IsZero()
    {
        Assert.Equal(0, Size.Zero.Width);
        Assert.Equal(0, Size.Zero.Height);
    }

    [Fact]
    public void Size_Equality_Works()
    {
        var s1 = new Size(10, 20);
        var s2 = new Size(10, 20);
        var s3 = new Size(10, 30);
        
        Assert.Equal(s1, s2);
        Assert.NotEqual(s1, s3);
        Assert.True(s1 == s2);
        Assert.True(s1 != s3);
    }

    #endregion

    #region Rect Tests

    [Fact]
    public void Rect_Constructor_SetsProperties()
    {
        var rect = new Rect(5, 10, 100, 50);
        
        Assert.Equal(5, rect.X);
        Assert.Equal(10, rect.Y);
        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
    }

    [Fact]
    public void Rect_Right_IsXPlusWidth()
    {
        var rect = new Rect(10, 0, 50, 20);
        
        Assert.Equal(60, rect.Right);
    }

    [Fact]
    public void Rect_Bottom_IsYPlusHeight()
    {
        var rect = new Rect(0, 10, 20, 30);
        
        Assert.Equal(40, rect.Bottom);
    }

    [Fact]
    public void Rect_Size_ReturnsCorrectSize()
    {
        var rect = new Rect(5, 5, 100, 50);
        
        Assert.Equal(new Size(100, 50), rect.Size);
    }

    [Fact]
    public void Rect_FromSize_CreatesRectAtOrigin()
    {
        var size = new Size(80, 24);
        var rect = Rect.FromSize(size);
        
        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(80, rect.Width);
        Assert.Equal(24, rect.Height);
    }

    [Fact]
    public void Rect_Equality_Works()
    {
        var r1 = new Rect(1, 2, 3, 4);
        var r2 = new Rect(1, 2, 3, 4);
        var r3 = new Rect(1, 2, 3, 5);
        
        Assert.Equal(r1, r2);
        Assert.NotEqual(r1, r3);
    }

    #endregion

    #region Constraints Tests

    [Fact]
    public void Constraints_Constructor_SetsProperties()
    {
        var c = new Constraints(10, 100, 5, 50);
        
        Assert.Equal(10, c.MinWidth);
        Assert.Equal(100, c.MaxWidth);
        Assert.Equal(5, c.MinHeight);
        Assert.Equal(50, c.MaxHeight);
    }

    [Fact]
    public void Constraints_Unbounded_HasMaxValues()
    {
        var c = Constraints.Unbounded;
        
        Assert.Equal(0, c.MinWidth);
        Assert.Equal(int.MaxValue, c.MaxWidth);
        Assert.Equal(0, c.MinHeight);
        Assert.Equal(int.MaxValue, c.MaxHeight);
    }

    [Fact]
    public void Constraints_Tight_HasEqualMinMax()
    {
        var c = Constraints.Tight(80, 24);
        
        Assert.Equal(80, c.MinWidth);
        Assert.Equal(80, c.MaxWidth);
        Assert.Equal(24, c.MinHeight);
        Assert.Equal(24, c.MaxHeight);
    }

    [Fact]
    public void Constraints_Loose_HasZeroMin()
    {
        var c = Constraints.Loose(80, 24);
        
        Assert.Equal(0, c.MinWidth);
        Assert.Equal(80, c.MaxWidth);
        Assert.Equal(0, c.MinHeight);
        Assert.Equal(24, c.MaxHeight);
    }

    [Fact]
    public void Constraints_Constrain_ClampsToMin()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(5, 5);
        
        var result = c.Constrain(size);
        
        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void Constraints_Constrain_ClampsToMax()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(200, 200);
        
        var result = c.Constrain(size);
        
        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void Constraints_Constrain_PreservesWithinBounds()
    {
        var c = new Constraints(10, 100, 10, 100);
        var size = new Size(50, 50);
        
        var result = c.Constrain(size);
        
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void Constraints_WithWidth_SetsFixedWidth()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithWidth(50);
        
        Assert.Equal(50, result.MinWidth);
        Assert.Equal(50, result.MaxWidth);
        Assert.Equal(0, result.MinHeight);
        Assert.Equal(100, result.MaxHeight);
    }

    [Fact]
    public void Constraints_WithHeight_SetsFixedHeight()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithHeight(50);
        
        Assert.Equal(0, result.MinWidth);
        Assert.Equal(100, result.MaxWidth);
        Assert.Equal(50, result.MinHeight);
        Assert.Equal(50, result.MaxHeight);
    }

    [Fact]
    public void Constraints_WithMaxWidth_ReducesMax()
    {
        var c = Constraints.Loose(100, 100);
        var result = c.WithMaxWidth(50);
        
        Assert.Equal(0, result.MinWidth);
        Assert.Equal(50, result.MaxWidth);
    }

    [Fact]
    public void Constraints_WithMaxWidth_DoesNotIncrease()
    {
        var c = Constraints.Loose(50, 50);
        var result = c.WithMaxWidth(100);
        
        Assert.Equal(50, result.MaxWidth);
    }

    [Fact]
    public void Constraints_Equality_Works()
    {
        var c1 = new Constraints(1, 2, 3, 4);
        var c2 = new Constraints(1, 2, 3, 4);
        var c3 = new Constraints(1, 2, 3, 5);
        
        Assert.Equal(c1, c2);
        Assert.NotEqual(c1, c3);
    }

    #endregion
}
