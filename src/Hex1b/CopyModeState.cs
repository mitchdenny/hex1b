namespace Hex1b;

/// <summary>
/// Represents the current state of copy mode on a terminal widget.
/// </summary>
public enum CopyModeState
{
    /// <summary>Copy mode is not active.</summary>
    Inactive,

    /// <summary>Copy mode is active but no selection has been started.</summary>
    Active,

    /// <summary>Copy mode is active with character-level selection.</summary>
    CharacterSelection,

    /// <summary>Copy mode is active with line-level selection.</summary>
    LineSelection,

    /// <summary>Copy mode is active with block/rectangular selection.</summary>
    BlockSelection
}
