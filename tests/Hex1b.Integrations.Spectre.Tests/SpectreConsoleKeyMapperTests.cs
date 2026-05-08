using Hex1b.Input;
using Hex1b.Integrations.Spectre.SpectreConsole;

namespace Hex1b.Integrations.Spectre.Tests;

public class SpectreConsoleKeyMapperTests
{
    public static TheoryData<Hex1bKey, ConsoleKey> KeyMappings()
    {
        var data = new TheoryData<Hex1bKey, ConsoleKey>
        {
            { Hex1bKey.A, ConsoleKey.A },
            { Hex1bKey.Z, ConsoleKey.Z },
            { Hex1bKey.D0, ConsoleKey.D0 },
            { Hex1bKey.D9, ConsoleKey.D9 },
            { Hex1bKey.F1, ConsoleKey.F1 },
            { Hex1bKey.F12, ConsoleKey.F12 },
            { Hex1bKey.UpArrow, ConsoleKey.UpArrow },
            { Hex1bKey.DownArrow, ConsoleKey.DownArrow },
            { Hex1bKey.LeftArrow, ConsoleKey.LeftArrow },
            { Hex1bKey.RightArrow, ConsoleKey.RightArrow },
            { Hex1bKey.Home, ConsoleKey.Home },
            { Hex1bKey.End, ConsoleKey.End },
            { Hex1bKey.PageUp, ConsoleKey.PageUp },
            { Hex1bKey.PageDown, ConsoleKey.PageDown },
            { Hex1bKey.Backspace, ConsoleKey.Backspace },
            { Hex1bKey.Delete, ConsoleKey.Delete },
            { Hex1bKey.Insert, ConsoleKey.Insert },
            { Hex1bKey.Tab, ConsoleKey.Tab },
            { Hex1bKey.Enter, ConsoleKey.Enter },
            { Hex1bKey.Spacebar, ConsoleKey.Spacebar },
            { Hex1bKey.Escape, ConsoleKey.Escape },
            { Hex1bKey.NumPad0, ConsoleKey.NumPad0 },
            { Hex1bKey.NumPad9, ConsoleKey.NumPad9 },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(KeyMappings))]
    public void ToConsoleKey_RoundTripsCanonicalKeys(Hex1bKey input, ConsoleKey expected)
    {
        var actual = SpectreConsoleKeyMapper.ToConsoleKey(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToConsoleKey_UnknownKey_ReturnsNoName()
    {
        Assert.Equal(ConsoleKey.NoName, SpectreConsoleKeyMapper.ToConsoleKey(Hex1bKey.None));
    }

    [Fact]
    public void ToConsoleKeyInfo_PrintableLetter_PreservesCharacter()
    {
        var evt = new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None);

        var info = SpectreConsoleKeyMapper.ToConsoleKeyInfo(evt);

        Assert.NotNull(info);
        Assert.Equal('a', info!.Value.KeyChar);
        Assert.Equal(ConsoleKey.A, info.Value.Key);
        Assert.Equal((ConsoleModifiers)0, info.Value.Modifiers);
    }

    [Fact]
    public void ToConsoleKeyInfo_AllModifiers_AreFlagged()
    {
        var evt = new Hex1bKeyEvent(
            Hex1bKey.X,
            'X',
            Hex1bModifiers.Shift | Hex1bModifiers.Alt | Hex1bModifiers.Control);

        var info = SpectreConsoleKeyMapper.ToConsoleKeyInfo(evt);

        Assert.NotNull(info);
        Assert.True((info!.Value.Modifiers & ConsoleModifiers.Shift) != 0);
        Assert.True((info.Value.Modifiers & ConsoleModifiers.Alt) != 0);
        Assert.True((info.Value.Modifiers & ConsoleModifiers.Control) != 0);
    }

    [Fact]
    public void ToConsoleKeyInfo_EmptyEvent_ReturnsNull()
    {
        var evt = new Hex1bKeyEvent(Hex1bKey.None, "", Hex1bModifiers.None);

        var info = SpectreConsoleKeyMapper.ToConsoleKeyInfo(evt);

        Assert.Null(info);
    }

    [Fact]
    public void ToConsoleKeyInfo_UnknownKeyButPrintableChar_ReturnsInfo()
    {
        // e.g. paste text — we still want Spectre to see the character.
        var evt = new Hex1bKeyEvent(Hex1bKey.None, "x", Hex1bModifiers.None);

        var info = SpectreConsoleKeyMapper.ToConsoleKeyInfo(evt);

        Assert.NotNull(info);
        Assert.Equal('x', info!.Value.KeyChar);
        Assert.Equal(ConsoleKey.NoName, info.Value.Key);
    }
}
