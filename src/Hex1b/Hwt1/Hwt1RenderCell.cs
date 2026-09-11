namespace Hex1b;

internal readonly record struct Hwt1RenderCell(
    string Text, uint Foreground, uint Background, uint UnderlineColor,
    ushort Attributes, byte Width, byte UnderlineStyle);
