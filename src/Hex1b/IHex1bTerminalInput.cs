using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Abstraction for terminal input operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface IHex1bTerminalInput
{
    /// <summary>
    /// Reads input events asynchronously. The channel completes when the terminal closes.
    /// Events include keyboard input, resize events, and terminal capability responses.
    /// </summary>
    ChannelReader<Hex1bEvent> InputEvents { get; }
}
