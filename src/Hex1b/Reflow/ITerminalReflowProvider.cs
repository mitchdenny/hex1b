namespace Hex1b.Reflow;

/// <summary>
/// Opt-in interface that presentation adapters can implement to provide
/// terminal-emulator-specific reflow behavior during resize operations.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="Hex1bTerminal"/> resizes, it checks if its presentation adapter
/// implements this interface. If so, it calls <see cref="Reflow"/> with the current
/// buffer state and applies the returned result. If the adapter does not implement
/// this interface, the terminal falls back to simple crop-and-extend behavior.
/// </para>
/// <para>
/// Different terminal emulators (xterm, kitty, alacritty) handle reflow differently.
/// The adapter controls the reflow algorithm — <see cref="Hex1bTerminal"/> is fully
/// decoupled from how reflow works.
/// </para>
/// <para>
/// Pre-built strategies are available for adapters to delegate to.
/// </para>
/// </remarks>
public interface ITerminalReflowProvider
{
    /// <summary>
    /// Gets whether reflow is enabled. When <c>false</c>, the terminal uses
    /// standard crop-and-extend resize behavior even though the adapter
    /// implements this interface. Defaults to <c>true</c>.
    /// </summary>
    bool ReflowEnabled => true;

    /// <summary>
    /// Performs reflow of terminal content during a resize operation.
    /// </summary>
    /// <param name="context">The current terminal state including screen buffer, scrollback, and cursor position.</param>
    /// <returns>The reflowed terminal state with new buffer, scrollback, and cursor position.</returns>
    ReflowResult Reflow(ReflowContext context);

    /// <summary>
    /// Gets whether absolute cursor positioning (CUP, HVP) should clear the
    /// <see cref="CellAttributes.SoftWrap"/> flag on the current row's last cell.
    /// </summary>
    /// <remarks>
    /// Most terminal emulators break the reflow chain when absolute positioning is used,
    /// because it indicates the application is managing screen layout directly.
    /// When <c>true</c>, the terminal clears <see cref="CellAttributes.SoftWrap"/> from
    /// the last cell of the row before moving the cursor.
    /// </remarks>
    bool ShouldClearSoftWrapOnAbsolutePosition { get; }
}
