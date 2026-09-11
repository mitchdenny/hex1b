namespace Hex1b.Input;

internal readonly record struct TerminalMouseInput(
    TerminalMouseButton Button,
    MouseAction Action,
    int X,
    int Y,
    Hex1bModifiers Modifiers,
    int Count = 1);
