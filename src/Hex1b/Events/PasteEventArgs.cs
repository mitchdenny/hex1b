using Hex1b.Input;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for paste events.
/// Provides streaming access to the pasted content via <see cref="Paste"/>.
/// </summary>
/// <remarks>
/// Unlike other event args, paste events do not carry an <see cref="InputBindingActionContext"/>
/// because paste handlers run concurrently with the render loop on a background thread.
/// </remarks>
public sealed class PasteEventArgs
{
    /// <summary>
    /// The paste context providing streaming access to the pasted content.
    /// </summary>
    public PasteContext Paste { get; }

    internal PasteEventArgs(PasteContext paste)
    {
        Paste = paste ?? throw new ArgumentNullException(nameof(paste));
    }
}
