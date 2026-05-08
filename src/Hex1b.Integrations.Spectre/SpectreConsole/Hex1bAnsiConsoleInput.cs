using System.Threading.Channels;
using Hex1b;
using Hex1b.Input;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// An <see cref="IAnsiConsoleInput"/> that drains
/// <see cref="Hex1bKeyEvent"/>s from an <see cref="IHex1bAppTerminalWorkloadAdapter"/>'s
/// input channel and exposes them to Spectre.Console as
/// <see cref="ConsoleKeyInfo"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Non-key events flowing through the channel (resize, mouse, paste) are
/// drained and discarded — Spectre.Console has no model for those, and
/// leaving them blocking the channel would starve subsequent key reads.
/// </para>
/// <para>
/// The reader is shared safely across calls but expects a single consumer
/// (Spectre's prompt or live-display loop), matching the channel's
/// single-reader configuration.
/// </para>
/// </remarks>
public sealed class Hex1bAnsiConsoleInput : IAnsiConsoleInput
{
    private readonly ChannelReader<Hex1bEvent> _reader;

    /// <summary>
    /// Initializes a new <see cref="Hex1bAnsiConsoleInput"/> drawing key
    /// events from the supplied workload adapter.
    /// </summary>
    public Hex1bAnsiConsoleInput(IHex1bAppTerminalWorkloadAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _reader = adapter.InputEvents;
    }

    /// <inheritdoc />
    public bool IsKeyAvailable()
    {
        // Peek by trying to read; if a non-key event surfaces, drop it and
        // try again until either a key event is at the head of the queue or
        // the queue is empty.
        while (_reader.TryPeek(out var evt))
        {
            if (evt is Hex1bKeyEvent)
            {
                return true;
            }

            if (!_reader.TryRead(out _))
            {
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        while (_reader.TryRead(out var evt))
        {
            if (evt is Hex1bKeyEvent keyEvent)
            {
                var mapped = SpectreConsoleKeyMapper.ToConsoleKeyInfo(keyEvent);
                if (mapped is not null)
                {
                    return mapped;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        while (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_reader.TryRead(out var evt))
            {
                if (evt is not Hex1bKeyEvent keyEvent)
                {
                    continue;
                }

                var mapped = SpectreConsoleKeyMapper.ToConsoleKeyInfo(keyEvent);
                if (mapped is not null)
                {
                    return mapped;
                }
            }
        }

        return null;
    }
}
