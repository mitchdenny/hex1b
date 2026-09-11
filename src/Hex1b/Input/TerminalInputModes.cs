namespace Hex1b.Input;

internal readonly record struct TerminalInputModes(
    bool ApplicationCursorKeys,
    bool BracketedPaste,
    TerminalMouseTracking MouseTracking,
    TerminalMouseEncoding MouseEncoding,
    int Width,
    int Height);
