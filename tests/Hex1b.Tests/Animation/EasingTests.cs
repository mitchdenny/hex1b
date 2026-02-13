using Hex1b.Animation;

namespace Hex1b.Tests.Animation;

public class EasingTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void Linear_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.Linear(input), 6);
    }

    [Fact]
    public void Linear_AtMidpoint_Returns05()
    {
        Assert.Equal(0.5, Easing.Linear(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseInQuad_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseInQuad(input), 6);
    }

    [Fact]
    public void EaseInQuad_AtMidpoint_Returns025()
    {
        // 0.5^2 = 0.25
        Assert.Equal(0.25, Easing.EaseInQuad(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseOutQuad_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseOutQuad(input), 6);
    }

    [Fact]
    public void EaseOutQuad_AtMidpoint_Returns075()
    {
        // 0.5 * (2 - 0.5) = 0.75
        Assert.Equal(0.75, Easing.EaseOutQuad(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseInOutQuad_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseInOutQuad(input), 6);
    }

    [Fact]
    public void EaseInOutQuad_AtMidpoint_Returns05()
    {
        Assert.Equal(0.5, Easing.EaseInOutQuad(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseInCubic_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseInCubic(input), 6);
    }

    [Fact]
    public void EaseInCubic_AtMidpoint_Returns0125()
    {
        // 0.5^3 = 0.125
        Assert.Equal(0.125, Easing.EaseInCubic(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseOutCubic_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseOutCubic(input), 6);
    }

    [Fact]
    public void EaseOutCubic_AtMidpoint_Returns0875()
    {
        // (0.5-1)^3 + 1 = -0.125 + 1 = 0.875
        Assert.Equal(0.875, Easing.EaseOutCubic(0.5), 6);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void EaseInOutCubic_AtBoundaries(double input, double expected)
    {
        Assert.Equal(expected, Easing.EaseInOutCubic(input), 6);
    }

    [Fact]
    public void EaseInOutCubic_AtMidpoint_Returns05()
    {
        Assert.Equal(0.5, Easing.EaseInOutCubic(0.5), 6);
    }

    [Fact]
    public void AllEasings_AreMonotonicallyIncreasing()
    {
        var easings = new[]
        {
            Easing.Linear,
            Easing.EaseInQuad, Easing.EaseOutQuad, Easing.EaseInOutQuad,
            Easing.EaseInCubic, Easing.EaseOutCubic, Easing.EaseInOutCubic,
        };

        foreach (var easing in easings)
        {
            var prev = easing(0);
            for (var t = 0.01; t <= 1.0; t += 0.01)
            {
                var current = easing(t);
                Assert.True(current >= prev - 1e-10,
                    $"Easing not monotonic at t={t}: {prev} -> {current}");
                prev = current;
            }
        }
    }
}
