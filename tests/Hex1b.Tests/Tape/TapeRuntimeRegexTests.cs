using Hex1b.Automation;

namespace Hex1b.Tests;

[TestClass]
public class TapeRuntimeRegexTests
{
    [TestMethod]
    [DataRow(@"\d", "1", true)]
    [DataRow(@"\d", "\u0661", false)]
    [DataRow(@"\w", "_", true)]
    [DataRow(@"\w", "\u00e9", false)]
    [DataRow(@"\s", "\t", true)]
    [DataRow(@"\s", "\u00a0", false)]
    [DataRow("done$", "done\n", false)]
    [DataRow("done$", "done", true)]
    [DataRow("(?P<word>done)", "done", true)]
    [DataRow("^.$", "\U0001F680", true)]
    [DataRow("^..$", "\U0001F680", false)]
    [DataRow(@"^\D{2}$", "\U0001F680", false)]
    [DataRow("^[^x]$", "\U0001F680", true)]
    [DataRow("^[a-z]$", "a", true)]
    [DataRow("^[a-z]$", "\U0001F680", false)]
    [DataRow("^\U0001F680+$", "\U0001F680\U0001F680", true)]
    [DataRow("(?m)^ready$", "before\nready\nafter", true)]
    [DataRow("(?s)^.$", "\n", true)]
    [DataRow("^(?s:.).$", "\n\n", false)]
    [DataRow(@"^\400$", "\u0100", true)]
    [DataRow(@"^\400$", "\0", false)]
    [DataRow(@"^\777$", "\u01ff", true)]
    [DataRow(@"^\777$", "\u00ff", false)]
    [DataRow(@"^[\400]$", "\u0100", true)]
    [DataRow(@"^[^\400]$", "\u0100", false)]
    [DataRow(@"^[^\400]$", "\0", true)]
    public void Compile_CompatibleSubset_PreservesGoMatching(string pattern, string input, bool matches)
    {
        Assert.IsTrue(TapeRuntimeRegex.TryCompile(pattern, out var regex, out var error), error);
        Assert.AreEqual(matches, regex!.IsMatch(input));
    }

    [TestMethod]
    [DataRow(@"\bready\b")]
    [DataRow("(?U)ready.*")]
    [DataRow("[[:alpha:]]")]
    [DataRow(@"[\W]")]
    [DataRow(@"\p{Greek}")]
    [DataRow("(?i)^s$")]
    [DataRow("(?i)^[^s]$")]
    [DataRow("(?i)^\U00010400$")]
    public void Compile_UnsupportedGoPattern_RejectsInsteadOfChangingSemantics(string pattern)
    {
        Assert.IsFalse(TapeRuntimeRegex.TryCompile(pattern, out var regex, out var error));
        Assert.IsNull(regex);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }
}
