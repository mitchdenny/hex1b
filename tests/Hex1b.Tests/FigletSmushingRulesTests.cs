using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Per-rule unit tests for <see cref="FigletSmushingRules"/>. Each test exercises a single rule
/// with a hand-picked character pair and verifies the produced sub-character matches the FIGfont
/// 2.0 specification.
/// </summary>
public class FigletSmushingRulesTests
{
    private const char Hb = '$';

    // ----- Universal smushing (rules == 0) --------------------------------------------

    [Fact]
    public void Universal_Blanks_YieldOther()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal(' ', 'A', Hb, 0, out var c));
        Assert.Equal('A', c);
        Assert.True(FigletSmushingRules.TrySmushHorizontal('A', ' ', Hb, 0, out c));
        Assert.Equal('A', c);
    }

    [Fact]
    public void Universal_TwoVisibles_RightWins()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('A', 'B', Hb, 0, out var c));
        Assert.Equal('B', c);
    }

    [Fact]
    public void Universal_HardblankBoth_KeepsHardblank()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 0, out var c));
        Assert.Equal(Hb, c);
    }

    [Fact]
    public void Universal_HardblankAndVisible_Rejects()
    {
        Assert.False(FigletSmushingRules.TrySmushHorizontal(Hb, 'A', Hb, 0, out _));
        Assert.False(FigletSmushingRules.TrySmushHorizontal('A', Hb, Hb, 0, out _));
    }

    // ----- Controlled rule 1: equal character ------------------------------------------

    [Fact]
    public void Equal_TwoIdenticalVisibles_Smushes()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('|', '|', Hb, 1, out var c));
        Assert.Equal('|', c);
    }

    [Fact]
    public void Equal_TwoHardblanks_Rejects_WhenRule6Off()
    {
        // Rule 1 must NOT smush hardblanks (only rule 6 does).
        Assert.False(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 1, out _));
    }

    [Fact]
    public void Equal_TwoHardblanks_Smushes_WhenRule6On()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal(Hb, Hb, Hb, 32, out var c));
        Assert.Equal(Hb, c);
    }

    // ----- Controlled rule 2: underscore -----------------------------------------------

    [Theory]
    [InlineData('|')]
    [InlineData('/')]
    [InlineData('\\')]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('(')]
    [InlineData(')')]
    [InlineData('<')]
    [InlineData('>')]
    public void Underscore_ReplacesUnderscoreWithReplacers(char other)
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('_', other, Hb, 2, out var c));
        Assert.Equal(other, c);
        Assert.True(FigletSmushingRules.TrySmushHorizontal(other, '_', Hb, 2, out c));
        Assert.Equal(other, c);
    }

    [Fact]
    public void Underscore_DoesNotSmush_WithRandomChar()
    {
        Assert.False(FigletSmushingRules.TrySmushHorizontal('_', 'a', Hb, 2, out _));
    }

    // ----- Controlled rule 3: hierarchy -----------------------------------------------

    [Fact]
    public void Hierarchy_DifferentClasses_HigherWins()
    {
        // |=0, /\=1, []=2, {}=3, ()=4, <>=5
        Assert.True(FigletSmushingRules.TrySmushHorizontal('|', '/', Hb, 4, out var c));
        Assert.Equal('/', c);
        Assert.True(FigletSmushingRules.TrySmushHorizontal('<', '|', Hb, 4, out c));
        Assert.Equal('<', c);
    }

    [Fact]
    public void Hierarchy_SameClass_DoesNotSmush()
    {
        Assert.False(FigletSmushingRules.TrySmushHorizontal('/', '\\', Hb, 4, out _));
    }

    // ----- Controlled rule 4: opposite pair --------------------------------------------

    [Theory]
    [InlineData('[', ']')]
    [InlineData(']', '[')]
    [InlineData('{', '}')]
    [InlineData('}', '{')]
    [InlineData('(', ')')]
    [InlineData(')', '(')]
    public void Opposite_BracesParenBrackets_BecomePipe(char a, char b)
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal(a, b, Hb, 8, out var c));
        Assert.Equal('|', c);
    }

    // ----- Controlled rule 5: big X ---------------------------------------------------

    [Fact]
    public void BigX_ForwardSlashBackslash_BecomesPipe()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('/', '\\', Hb, 16, out var c));
        Assert.Equal('|', c);
    }

    [Fact]
    public void BigX_BackslashForwardSlash_BecomesY()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('\\', '/', Hb, 16, out var c));
        Assert.Equal('Y', c);
    }

    [Fact]
    public void BigX_GreaterLess_BecomesX()
    {
        Assert.True(FigletSmushingRules.TrySmushHorizontal('>', '<', Hb, 16, out var c));
        Assert.Equal('X', c);
    }

    [Fact]
    public void BigX_LessGreater_DoesNotSmush()
    {
        // Spec note: "<>" is NOT smushed.
        Assert.False(FigletSmushingRules.TrySmushHorizontal('<', '>', Hb, 16, out _));
    }

    // ----- Vertical rules --------------------------------------------------------------

    [Fact]
    public void Vertical_Blanks_YieldOther()
    {
        Assert.True(FigletSmushingRules.TrySmushVertical(' ', 'X', Hb, 0, out var c));
        Assert.Equal('X', c);
    }

    [Fact]
    public void Vertical_HardblankActsAsBlank()
    {
        Assert.True(FigletSmushingRules.TrySmushVertical(Hb, 'X', Hb, 0, out var c));
        Assert.Equal('X', c);
    }

    [Fact]
    public void Vertical_Universal_LowerWins()
    {
        Assert.True(FigletSmushingRules.TrySmushVertical('A', 'B', Hb, 0, out var c));
        Assert.Equal('B', c);
    }

    [Fact]
    public void Vertical_HorizontalLine_DashOverUnderscore_BecomesEquals()
    {
        Assert.True(FigletSmushingRules.TrySmushVertical('-', '_', Hb, FigletSmushingRules.VerticalRuleHorizontalLine, out var c));
        Assert.Equal('=', c);
        Assert.True(FigletSmushingRules.TrySmushVertical('_', '-', Hb, FigletSmushingRules.VerticalRuleHorizontalLine, out c));
        Assert.Equal('=', c);
    }
}
