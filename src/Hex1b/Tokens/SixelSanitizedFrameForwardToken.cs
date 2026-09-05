namespace Hex1b.Tokens;

/// <summary>
/// Carries the bounded, verbatim wire bytes of a Sixel DCS sequence whose framing
/// was cancelled, left unterminated, or exceeded the retention limit, for the sole
/// purpose of forwarding it while the opt-in <see cref="Hex1b.Sixel.SixelSanitizationPolicy"/>
/// is configured not to suppress that outcome.
/// </summary>
/// <param name="WireBytes">
/// The complete <c>ESC P ... ESC \</c> sequence to serialize verbatim, reconstructed
/// from the byte-stream framer's bounded retained content
/// (<see cref="Hex1b.Sixel.SixelSanitizationPolicy"/> never sees more than the
/// configured retention limit — this is "bounded" buffered forwarding, not a
/// guarantee of reproducing every original byte of an oversized sequence).
/// </param>
/// <remarks>
/// This exists only because these frames never produce a <see cref="DcsToken"/> in
/// the first place (see <see cref="Hex1bTerminal.TokenizeRawWorkloadOutput"/>), so
/// there would otherwise be no way to opt back into forwarding them once sanitization
/// disables immediate raw-byte passthrough. It carries no Sixel semantics whatsoever
/// — it is never registered in a batch's framed-DCS map, is a complete no-op wherever
/// tokens are applied to terminal state (see <see cref="Hex1bTerminal.ApplyTokens"/>),
/// and is filtered out just like any other Sixel wire bytes when the effective route
/// does not deliver raw Sixel bytes at all. This type is intentionally internal: it
/// is translation/sanitization bookkeeping, not part of the public token surface a
/// host or filter is expected to recognize.
/// </remarks>
internal sealed record SixelSanitizedFrameForwardToken(ReadOnlyMemory<byte> WireBytes) : AnsiToken;
