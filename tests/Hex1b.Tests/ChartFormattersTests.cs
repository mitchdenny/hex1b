using Hex1b.Charts;

namespace Hex1b.Tests;

[TestClass]
public class ChartFormattersTests
{
    [TestMethod]
    public void FormatValue_Zero_ReturnsZero()
    {
        Assert.AreEqual("0", ChartFormatters.FormatValue(0));
    }

    [TestMethod]
    [DataRow(1, "1")]
    [DataRow(42, "42")]
    [DataRow(999, "999")]
    [DataRow(1234, "1,234")]
    [DataRow(9999, "9,999")]
    public void FormatValue_WholeNumbersUnder10K_UsesGroupedFormat(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(10000, "10.0K")]
    [DataRow(12345, "12.3K")]
    [DataRow(100000, "100K")]
    [DataRow(999999, "1,000K")]
    public void FormatValue_TensOfThousands_UsesSuffixK(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(1000000, "1.0M")]
    [DataRow(1500000, "1.5M")]
    [DataRow(12345678, "12.3M")]
    [DataRow(999999999, "1,000M")]
    public void FormatValue_Millions_UsesSuffixM(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(1000000000, "1.0B")]
    [DataRow(2500000000, "2.5B")]
    [DataRow(123456789012, "123B")]
    public void FormatValue_Billions_UsesSuffixB(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(-42, "-42")]
    [DataRow(-1234, "-1,234")]
    [DataRow(-12345, "-12.3K")]
    [DataRow(-1500000, "-1.5M")]
    public void FormatValue_NegativeValues_IncludesSign(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(3.14, "3.1")]
    [DataRow(42.5, "42.5")]
    [DataRow(1234.5, "1,234.5")]
    public void FormatValue_FractionalAboveOne_ShowsOneDecimal(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }

    [TestMethod]
    [DataRow(0.5, "0.5")]
    [DataRow(0.42, "0.42")]
    [DataRow(0.001, "0.001")]
    public void FormatValue_SmallFractions_UsesSignificantDigits(double value, string expected)
    {
        Assert.AreEqual(expected, ChartFormatters.FormatValue(value));
    }
}
