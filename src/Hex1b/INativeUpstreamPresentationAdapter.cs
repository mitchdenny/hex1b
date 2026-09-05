namespace Hex1b;

/// <summary>
/// Optional capability for presentation adapters whose upstream endpoint is a real
/// terminal emulator that autonomously answers protocol queries — such as Primary
/// Device Attributes (<c>CSI c</c>) and window operations (<c>CSI Ps t</c>) — because
/// raw bytes flow through to it unmodified.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to tell <see cref="Hex1bTerminal"/> that it must not
/// synthesize its own replies to these protocol queries: the real upstream terminal
/// already answers them directly over the same raw byte channel, and a synthetic
/// reply from Hex1b would arrive as a duplicate, conflicting response in the hosted
/// workload's input stream.
/// </para>
/// <para>
/// Presentation adapters that do <em>not</em> implement this interface are assumed to
/// have no independent terminal emulator behind them — for example headless test
/// harnesses, managed WebSocket/browser presentations, or (once
/// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see> lands)
/// translated raster-graphics presentations. For those, <see cref="Hex1bTerminal"/>
/// owns query answering and synthesizes replies from its own authoritative model.
/// </para>
/// <para>
/// <see cref="ConsolePresentationAdapter"/> implements this interface because it
/// connects Hex1b directly to a real terminal's raw stdin/stdout: any DA1 or window
/// operation query a hosted workload writes reaches the real terminal untouched, and
/// the real terminal's reply is forwarded back untouched in turn.
/// </para>
/// </remarks>
public interface INativeUpstreamPresentationAdapter : IHex1bTerminalPresentationAdapter
{
}
