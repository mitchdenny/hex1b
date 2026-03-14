using Hex1b.Charts;
using Xunit;

namespace Hex1b.Tests;

public class ChartFormattersTests
{
    [Fact]
    public void FormatValue_Zero_ReturnsZero()
    {
        Assert.Equal("0", ChartFormatters.FormatValue(0));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(42, "42")]
    [InlineData(999, "999")]
    [InlineData(1234, "1,234")]
    [InlineData(9999, "9,999")]
    public void FormatValue_WholeNumbersUnder10K_UsesGroupedFormat(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(10000, "10.0K")]
    [InlineData(12345, "12.3K")]
    [InlineData(100000, "100K")]
    [InlineData(999999, "1,000K")]
    public void FormatValue_TensOfThousands_UsesSuffixK(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(1000000, "1.0M")]
    [InlineData(1500000, "1.5M")]
    [InlineData(12345678, "12.3M")]
    [InlineData(999999999, "1,000M")]
    public void FormatValue_Millions_UsesSuffixM(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(1000000000, "1.0B")]
    [InlineData(2500000000, "2.5B")]
    [InlineData(123456789012, "123B")]
    public void FormatValue_Billions_UsesSuffixB(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(-42, "-42")]
    [InlineData(-1234, "-1,234")]
    [InlineData(-12345, "-12.3K")]
    [InlineData(-1500000, "-1.5M")]
    public void FormatValue_NegativeValues_IncludesSign(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(3.14, "3.1")]
    [InlineData(42.5, "42.5")]
    [InlineData(1234.5, "1,234.5")]
    public void FormatValue_FractionalAboveOne_ShowsOneDecimal(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(0.42, "0.42")]
    [InlineData(0.001, "0.001")]
    public void FormatValue_SmallFractions_UsesSignificantDigits(double value, string expected)
    {
        Assert.Equal(expected, ChartFormatters.FormatValue(value));
    }
}
