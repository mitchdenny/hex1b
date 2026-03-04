namespace Hex1b.Input;

/// <summary>
/// Event emitted when a bracketed paste sequence (ESC[200~ ... ESC[201~) is detected.
/// The <see cref="Paste"/> context provides streaming access to the paste data.
/// </summary>
/// <param name="Paste">The paste context providing streaming access to the pasted content.</param>
public sealed record Hex1bPasteEvent(PasteContext Paste) : Hex1bEvent;
