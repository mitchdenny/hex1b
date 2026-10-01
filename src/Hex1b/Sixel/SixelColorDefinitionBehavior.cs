using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies whether a color-register definition also selects that register.
/// </summary>
internal enum SixelColorDefinitionBehavior
{
    /// <summary>Defining a register also selects it, as specified by DEC.</summary>
    DefineAndSelect,

    /// <summary>Defining a register leaves the current selection unchanged.</summary>
    DefineOnly,
}
