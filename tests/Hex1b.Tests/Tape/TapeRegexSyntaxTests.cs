using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests;

[TestClass]
public class TapeRegexSyntaxTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("(?i)ready")]
    [DataRow("(?m)^ready$")]
    [DataRow("(?s:.)")]
    [DataRow("(?U)a+")]
    [DataRow("(?im-sU:a)")]
    [DataRow("(?-i)a")]
    [DataRow("(?P<name>abc)")]
    [DataRow("(?<name>abc)")]
    [DataRow("(?P<1>abc)")]
    [DataRow("(?P<name>a)(?P<name>b)")]
    [DataRow("(?:)")]
    [DataRow("a|")]
    [DataRow("|a")]
    [DataRow("a{1000}")]
    [DataRow("(a{10}){100}")]
    [DataRow("a{0,1000}?")]
    [DataRow("a{1,}")]
    [DataRow("a{01}")]
    [DataRow("a{word}")]
    [DataRow(@"\p{Greek}+")]
    [DataRow(@"\p{Han}")]
    [DataRow(@"\p{Kawi}")]
    [DataRow(@"\p{Nag_Mundari}")]
    [DataRow(@"\pL")]
    [DataRow(@"\p{Any}")]
    [DataRow(@"\P{^Greek}")]
    [DataRow(@"\p{^Lu}")]
    [DataRow(@"\A\d+\s\w\b\z")]
    [DataRow(@"\Q(.*\E")]
    [DataRow(@"\Qunterminated")]
    [DataRow(@"\_\/\!\#")]
    [DataRow(@"\a\f\t\n\r\v")]
    [DataRow(@"\0\07\123\12")]
    [DataRow(@"\x41\x{10FFFF}\x{00000000001}")]
    [DataRow(@"\x{D800}")]
    [DataRow("[[:alpha:][:^digit:]]")]
    [DataRow(@"[\p{Greek}\d]")]
    [DataRow("[]a]")]
    [DataRow("[^]a]")]
    [DataRow("[a-]")]
    [DataRow("[-a]")]
    [DataRow("[a-z]")]
    [DataRow(@"\b*")]
    public void Parse_GoRegexSyntax_AcceptsWithoutDotNetRestrictions(string pattern)
    {
        Assert.IsTrue(TapeGoRegexValidator.TryValidate(pattern, out var error), $"{pattern}: {error}");
        Assert.IsTrue(new TapeParser().TryParse($"Set WaitPattern `{pattern}`", out _, out var diagnostics),
            string.Join("; ", diagnostics.Select(d => d.Message)));
    }

    [TestMethod]
    [DataRow("(?=a)")]
    [DataRow("(?!a)")]
    [DataRow("(?<=a)")]
    [DataRow("(?<!a)")]
    [DataRow("(?>a)")]
    [DataRow("(?x)a")]
    [DataRow("(?n)a")]
    [DataRow("(?i-)")]
    [DataRow("(?-)")]
    [DataRow("(?)")]
    [DataRow("(?P<>)")]
    [DataRow("(?<invalid-name>a)")]
    [DataRow("(a")]
    [DataRow("a)")]
    [DataRow("(?#comment)a")]
    [DataRow(@"\1")]
    [DataRow(@"\8")]
    [DataRow(@"(a)\1")]
    [DataRow(@"\k<name>")]
    [DataRow(@"\g{1}")]
    [DataRow(@"\u0041")]
    [DataRow(@"\e")]
    [DataRow(@"\cA")]
    [DataRow(@"\R")]
    [DataRow(@"\Z")]
    [DataRow(@"\C")]
    [DataRow(@"\é")]
    [DataRow(@"\x1")]
    [DataRow(@"\x{}")]
    [DataRow(@"\x{110000}")]
    [DataRow(@"\p{NotAProperty}")]
    [DataRow(@"\p{Letter}")]
    [DataRow(@"\p{IsGreek}")]
    [DataRow(@"\p{Cn}")]
    [DataRow(@"[\b]")]
    [DataRow(@"[a-\d]")]
    [DataRow("[z-a]")]
    [DataRow("[[:unknown:]]")]
    [DataRow("[[:^^alpha:]]")]
    [DataRow("[]")]
    [DataRow("[")]
    [DataRow("*a")]
    [DataRow("a**")]
    [DataRow("a*+")]
    [DataRow("a++")]
    [DataRow("a???")]
    [DataRow("a{2,1}")]
    [DataRow("a{1001}")]
    [DataRow("a{1,1001}")]
    [DataRow("(a{1000}){2}")]
    [DataRow("{2}")]
    [DataRow("\\")]
    public void TryParse_NonGoRegexSyntax_Rejects(string pattern)
    {
        Assert.IsFalse(TapeGoRegexValidator.TryValidate(pattern, out _), pattern);
        Assert.IsFalse(new TapeParser().TryParse($"Set WaitPattern `{pattern}`", out _, out var diagnostics), pattern);
        Assert.IsTrue(diagnostics.Any(d => d.Code == "TAPE010"));
    }

    [TestMethod]
    [DataRow(@"Wait /foo\/bar/", @"foo\/bar")]
    [DataRow(@"Wait /foo\\/", @"foo\\")]
    [DataRow(@"Wait /foo\\\/bar/", @"foo\\\/bar")]
    [DataRow("Wait //", "")]
    [DataRow("Wait /unterminated", "unterminated")]
    [DataRow("Wait /a\\\nb/", "a\\\nb")]
    public void Parse_RegexDelimiterBackslashes_PreservesRawPattern(string text, string expected)
    {
        var command = TestSeq.IsType<TapeWaitCommand>(TestSeq.Single(new TapeParser().Parse(text).Commands));
        Assert.AreEqual(expected, command.Pattern);
    }

    [TestMethod]
    public void TryParse_ExcessiveRegexNesting_ReportsDiagnosticWithoutStackOverflow()
    {
        var pattern = new string('(', 2000) + "a" + new string(')', 2000);
        Assert.IsFalse(new TapeParser().TryParse($"Set WaitPattern `{pattern}`", out _));
    }
}
