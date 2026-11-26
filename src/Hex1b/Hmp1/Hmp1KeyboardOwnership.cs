namespace Hex1b;

/// <summary>Whether the HMP1 viewer's Kitty keyboard entry is on the physical host.</summary>
internal enum Hmp1KeyboardOwnership
{
    /// <summary>No entry has been pushed, or it was popped.</summary>
    None,

    /// <summary>The viewer's entry is the top of the host's current-screen stack.</summary>
    Owned,

    /// <summary>
    /// A write containing a push or pop failed after it may have been partly delivered.
    /// The viewer stops editing the host stack rather than risk popping a host entry.
    /// </summary>
    Unknown,
}
