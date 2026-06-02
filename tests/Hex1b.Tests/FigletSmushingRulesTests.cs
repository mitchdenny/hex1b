using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Per-rule unit tests for <see cref="FigletSmushingRules"/>. Each test exercises a single rule
/// with a hand-picked character pair and verifies the produced sub-character matches the FIGfont
/// 2.0 specification.
/// </summary>
[TestClass]
public class FigletSmushingRulesTests
{
    private const char Hb = '$';

    // ----- Universal smushing (rules == 0) --------------------------------------------

    [TestMethod]
    public void Universal_Blanks_YieldOther()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal(' ', 'A', Hb, 0, out var c));
        Assert.AreEqual('A', c);
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('A', ' ', Hb, 0, out c));
        Assert.AreEqual('A', c);
    }

    [TestMethod]
    public void Universal_TwoVisibles_RightWins()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('A', 'B', Hb, 0, out var c));
        Assert.AreEqual('B', c);
    }

    [TestMethod]
    public void Universal_HardblankBoth_KeepsHardblank()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 0, out var c));
        Assert.AreEqual(Hb, c);
    }

    [TestMethod]
    public void Universal_HardblankAndVisible_Rejects()
    {
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal(Hb, 'A', Hb, 0, out _));
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal('A', Hb, Hb, 0, out _));
    }

    // ----- Controlled rule 1: equal character ------------------------------------------

    [TestMethod]
    public void Equal_TwoIdenticalVisibles_Smushes()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('|', '|', Hb, 1, out var c));
        Assert.AreEqual('|', c);
    }

    [TestMethod]
    public void Equal_TwoHardblanks_Rejects_WhenRule6Off()
    {
        // Rule 1 must NOT smush hardblanks (only rule 6 does).
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 1, out _));
    }

    [TestMethod]
    public void Equal_TwoHardblanks_Smushes_WhenRule6On()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 32, out var c));
        Assert.AreEqual(Hb, c);
    }

    // ----- Controlled rule 2: underscore -----------------------------------------------

    [TestMethod]
    [DataRow('|')]
    [DataRow('/')]
    [DataRow('\\')]
    [DataRow('[')]
    [DataRow(']')]
    [DataRow('{')]
    [DataRow('}')]
    [DataRow('(')]
    [DataRow(')')]
    [DataRow('<')]
    [DataRow('>')]
    public void Underscore_ReplacesUnderscoreWithReplacers(char other)
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('_', other, Hb, 2, out var c));
        Assert.AreEqual(other, c);
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal(other, '_', Hb, 2, out c));
        Assert.AreEqual(other, c);
    }

    [TestMethod]
    public void Underscore_DoesNotSmush_WithRandomChar()
    {
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal('_', 'a', Hb, 2, out _));
    }

    // ----- Controlled rule 3: hierarchy -----------------------------------------------

    [TestMethod]
    public void Hierarchy_DifferentClasses_HigherWins()
    {
        // |=0, /\=1, []=2, {}=3, ()=4, <>=5
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('|', '/', Hb, 4, out var c));
        Assert.AreEqual('/', c);
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('<', '|', Hb, 4, out c));
        Assert.AreEqual('<', c);
    }

    [TestMethod]
    public void Hierarchy_SameClass_DoesNotSmush()
    {
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal('/', '\\', Hb, 4, out _));
    }

    // ----- Controlled rule 4: opposite pair --------------------------------------------

    [TestMethod]
    [DataRow('[', ']')]
    [DataRow(']', '[')]
    [DataRow('{', '}')]
    [DataRow('}', '{')]
    [DataRow('(', ')')]
    [DataRow(')', '(')]
    public void Opposite_BracesParenBrackets_BecomePipe(char a, char b)
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal(a, b, Hb, 8, out var c));
        Assert.AreEqual('|', c);
    }

    // ----- Controlled rule 5: big X ---------------------------------------------------

    [TestMethod]
    public void BigX_ForwardSlashBackslash_BecomesPipe()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('/', '\\', Hb, 16, out var c));
        Assert.AreEqual('|', c);
    }

    [TestMethod]
    public void BigX_BackslashForwardSlash_BecomesY()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('\\', '/', Hb, 16, out var c));
        Assert.AreEqual('Y', c);
    }

    [TestMethod]
    public void BigX_GreaterLess_BecomesX()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushHorizontal('>', '<', Hb, 16, out var c));
        Assert.AreEqual('X', c);
    }

    [TestMethod]
    public void BigX_LessGreater_DoesNotSmush()
    {
        // Spec note: "<>" is NOT smushed.
        Assert.IsFalse(FigletSmushingRules.TrySmushHorizontal('<', '>', Hb, 16, out _));
    }

    // ----- Vertical rules --------------------------------------------------------------

    [TestMethod]
    public void Vertical_Blanks_YieldOther()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushVertical(' ', 'X', Hb, 0, out var c));
        Assert.AreEqual('X', c);
    }

    [TestMethod]
    public void Vertical_HardblankActsAsBlank()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushVertical(Hb, 'X', Hb, 0, out var c));
        Assert.AreEqual('X', c);
    }

    [TestMethod]
    public void Vertical_Universal_LowerWins()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushVertical('A', 'B', Hb, 0, out var c));
        Assert.AreEqual('B', c);
    }

    [TestMethod]
    public void Vertical_HorizontalLine_DashOverUnderscore_BecomesEquals()
    {
        Assert.IsTrue(FigletSmushingRules.TrySmushVertical('-', '_', Hb, FigletSmushingRules.VerticalRuleHorizontalLine, out var c));
        Assert.AreEqual('=', c);
        Assert.IsTrue(FigletSmushingRules.TrySmushVertical('_', '-', Hb, FigletSmushingRules.VerticalRuleHorizontalLine, out c));
        Assert.AreEqual('=', c);
    }
}
