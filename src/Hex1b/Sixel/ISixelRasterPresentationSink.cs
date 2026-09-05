using Hex1b;

namespace Hex1b.Sixel;

/// <summary>
/// Optional presentation capability: receive an ordered, protocol-neutral stream of
/// Sixel raster events instead of (or in addition to) raw output bytes.
/// </summary>
/// <remarks>
/// <para>
/// Implement this alongside <see cref="Hex1b.IHex1bTerminalPresentationAdapter"/> when a
/// presentation wants to render Sixel graphics from Hex1b's authoritative model — for
/// example a managed WebSocket/delta client that maintains its own raster surface —
/// without reparsing the Sixel wire protocol itself. <see cref="Hex1bTerminal"/> detects
/// this interface on the active presentation and treats its presence as the
/// authoritative "managed raster sink" routing signal, taking priority over the raw
/// <see cref="Hex1b.TerminalCapabilities.SixelSupport"/> value for routing purposes (see
/// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>).
/// </para>
/// <para>
/// A presentation that also declares itself <see cref="Hex1b.Sixel.SixelPresentationSupport.Native"/>
/// still receives raw Sixel bytes via <see cref="Hex1b.IHex1bTerminalPresentationAdapter.WriteOutputAsync"/>
/// as usual (native byte-exact passthrough is unconditional and is never gated by this
/// interface); implementing this interface only adds the structured event stream on
/// top. A presentation that declares <see cref="Hex1b.Sixel.SixelPresentationSupport.Headless"/>
/// or does not participate in raw byte forwarding receives only the structured events.
/// </para>
/// <para>
/// Events for a given output batch are delivered via a single call to
/// <see cref="OnSixelRasterEventsAsync"/>, in an order consistent with the text/cell
/// output produced from the same batch — no separate, independently-ordered channel is
/// introduced. Implementations must not block the terminal's output pump for
/// longer than necessary; heavy rendering work should be handed off asynchronously.
/// </para>
/// </remarks>
public interface ISixelRasterPresentationSink : IHex1bTerminalPresentationAdapter
{
    /// <summary>
    /// Delivers the ordered raster events produced by processing one output batch.
    /// </summary>
    /// <param name="events">
    /// The events for this batch, in the order they should be applied. Never empty —
    /// this method is only invoked when there is at least one event to deliver.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask OnSixelRasterEventsAsync(IReadOnlyList<SixelRasterEvent> events, CancellationToken ct = default);
}
