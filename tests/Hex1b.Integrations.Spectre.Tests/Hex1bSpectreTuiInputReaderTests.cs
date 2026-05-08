using Hex1b;
using Hex1b.Input;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Tui.App;

namespace Hex1b.Integrations.Spectre.Tests;

public class Hex1bSpectreTuiInputReaderTests
{
    [Fact]
    public void Constructor_NullAdapter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Hex1bSpectreTuiInputReader(null!));
    }

    [Fact]
    public async Task Read_ReturnsKeyMessage_WhenChannelHasKeyEvent()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        using var reader = new Hex1bSpectreTuiInputReader(adapter);

        await adapter.WriteInputEventAsync(new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None));

        var message = await reader.Read(CancellationToken.None);

        var keyMessage = Assert.IsType<KeyMessage>(message);
        Assert.Equal(ConsoleKey.A, keyMessage.Info.Key);
        Assert.Equal('a', keyMessage.Info.KeyChar);
    }

    [Fact]
    public async Task Read_ReturnsNull_WhenChannelEmpty()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        using var reader = new Hex1bSpectreTuiInputReader(adapter);

        var message = await reader.Read(CancellationToken.None);

        Assert.Null(message);
    }

    [Fact]
    public async Task Read_SkipsNonKeyEvents()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        using var reader = new Hex1bSpectreTuiInputReader(adapter);

        // Push a resize event followed by a key event. The reader should
        // discard the resize and surface the key.
        await adapter.WriteInputEventAsync(new Hex1bResizeEvent(80, 24));
        await adapter.WriteInputEventAsync(new Hex1bKeyEvent(Hex1bKey.Enter, "\r", Hex1bModifiers.None));

        var message = await reader.Read(CancellationToken.None);

        var keyMessage = Assert.IsType<KeyMessage>(message);
        Assert.Equal(ConsoleKey.Enter, keyMessage.Info.Key);
    }

    [Fact]
    public async Task Read_ReturnsNull_WhenOnlyUnmappedKeyEventsPresent()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        using var reader = new Hex1bSpectreTuiInputReader(adapter);

        // A bare Hex1bKey.None with no character is unmapped — the mapper
        // returns null and the reader should drain it without surfacing.
        await adapter.WriteInputEventAsync(new Hex1bKeyEvent(Hex1bKey.None, string.Empty, Hex1bModifiers.None));

        var message = await reader.Read(CancellationToken.None);

        Assert.Null(message);
    }

    [Fact]
    public void Initialize_DoesNotThrow()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        using var reader = new Hex1bSpectreTuiInputReader(adapter);

        // We pass null because constructing a real ApplicationContext requires
        // an Application. Initialize is documented as a no-op so it must not
        // dereference the argument.
        reader.Initialize(null!);
    }
}
