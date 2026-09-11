# Experimental Sixel API

The Sixel widget and render-context APIs remain experimental under diagnostic
`HEX1B_SIXEL`.

Use `SixelPixelBuffer` with `SixelWidget` for structured application-generated
pixels. Use the pre-encoded string overload only as a compatibility path for
existing Sixel payloads; Hex1b validates and normalizes its DCS framing.
Structured pixels are resampled when layout requests an explicit cell span.
Pre-encoded payloads remain lossless and throw if their natural protocol-cell
span does not match the arranged widget size.

See the [SixelWidget guide](../../src/content/guide/widgets/sixel.md) for a
complete example, sizing and fallback behavior, and the native-terminal demo
command. The lower-level terminal behavior contract is documented in
[Sixel terminal behavior](../sixel-terminal-behavior.md).
