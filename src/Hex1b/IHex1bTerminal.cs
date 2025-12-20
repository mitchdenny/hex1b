using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Combined terminal interface for convenience.
/// </summary>
public interface IHex1bTerminal : IHex1bTerminalOutput, IHex1bTerminalInput
{
}
