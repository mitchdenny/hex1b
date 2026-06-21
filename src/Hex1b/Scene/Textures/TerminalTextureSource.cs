namespace Hex1b.Scene.Textures;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Adapts a live terminal into a buffer source that a <see cref="TerminalTexture"/> can
/// sample each frame. This is a thin, scene-side wrapper that <em>references</em> a
/// <see cref="TerminalWidgetHandle"/> and snapshots its screen buffer on demand.
/// </summary>
/// <remarks>
/// <para>
/// Keeping this as its own concrete type means the 3D/texture concern lives entirely in the
/// scene layer: the core terminal abstraction (<see cref="TerminalWidgetHandle"/>) exposes a
/// general-purpose <c>GetScreenBufferSnapshot()</c> but knows nothing about textures or
/// meshes. The typical flow is:
/// </para>
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithTerminalWidget(out var handle)
///     .Build();
/// _ = terminal.RunAsync(ct);
///
/// var texture = new TerminalTexture(new TerminalTextureSource(handle));
/// material.Texture = texture.Update(); // call once per frame
/// </code>
/// </remarks>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public sealed class TerminalTextureSource
{
    private readonly Func<(TerminalCell[,] Buffer, int Width, int Height)> _snapshot;

    /// <summary>
    /// Creates a source backed by a terminal widget handle. The handle's authoritative
    /// screen buffer is snapshotted each time <see cref="GetScreenBufferSnapshot"/> is called.
    /// </summary>
    /// <param name="handle">The terminal handle to sample.</param>
    public TerminalTextureSource(TerminalWidgetHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _snapshot = handle.GetScreenBufferSnapshot;
    }

    /// <summary>
    /// Creates a source backed by an arbitrary snapshot provider. Useful for feeding
    /// synthetic or pre-recorded buffers (for example in tests) without a live terminal.
    /// </summary>
    /// <param name="snapshotProvider">
    /// Returns the current cell buffer (indexed <c>[row, column]</c>) and its dimensions.
    /// </param>
    public TerminalTextureSource(Func<(TerminalCell[,] Buffer, int Width, int Height)> snapshotProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        _snapshot = snapshotProvider;
    }

    /// <summary>
    /// Gets an atomic snapshot of the current terminal screen buffer along with its
    /// dimensions. The returned buffer is indexed <c>[row, column]</c>.
    /// </summary>
    public (TerminalCell[,] Buffer, int Width, int Height) GetScreenBufferSnapshot() => _snapshot();
}
