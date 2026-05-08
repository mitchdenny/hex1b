using System.Threading.Channels;
using Hex1b;
using Hex1b.Input;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Tui.App;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// An <see cref="IInputReader"/> that converts <see cref="Hex1bKeyEvent"/>s
/// from a workload adapter's input channel into Spectre.Tui
/// <see cref="KeyMessage"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Returning <c>null</c> from <see cref="Read"/> tells Spectre.Tui's
/// <c>InputPump</c> to keep polling — that's how we cooperate with the
/// 60 fps render loop without consuming a CPU thread waiting on the channel.
/// </para>
/// <para>
/// Resize, mouse, and paste events are silently discarded because Spectre.Tui
/// has no model for them today. When Spectre.Tui grows mouse support this
/// reader will need a corresponding <c>MouseMessage</c> case.
/// </para>
/// </remarks>
public sealed class Hex1bSpectreTuiInputReader : IInputReader
{
    private readonly ChannelReader<Hex1bEvent> _reader;

    /// <summary>
    /// Initializes a new <see cref="Hex1bSpectreTuiInputReader"/> drawing key
    /// events from the supplied workload adapter.
    /// </summary>
    public Hex1bSpectreTuiInputReader(IHex1bAppTerminalWorkloadAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _reader = adapter.InputEvents;
    }

    /// <inheritdoc />
    /// <remarks>
    /// No-op. Hex1b owns Ctrl+C handling at the terminal level — wiring
    /// <see cref="Console.CancelKeyPress"/> here would fight the host
    /// process for the same signal.
    /// </remarks>
    public void Initialize(ApplicationContext application)
    {
    }

    /// <inheritdoc />
    public ValueTask<ApplicationMessage?> Read(CancellationToken cancellationToken)
    {
        while (_reader.TryRead(out var evt))
        {
            if (evt is not Hex1bKeyEvent keyEvent)
            {
                continue;
            }

            var info = SpectreConsoleKeyMapper.ToConsoleKeyInfo(keyEvent);
            if (info is not null)
            {
                return new ValueTask<ApplicationMessage?>(new KeyMessage(info.Value));
            }
        }

        return new ValueTask<ApplicationMessage?>((ApplicationMessage?)null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The underlying channel is owned by the workload adapter — we are
        // just a reader.
    }
}
