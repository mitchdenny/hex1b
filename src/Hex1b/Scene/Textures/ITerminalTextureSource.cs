namespace Hex1b.Scene.Textures;

/// <summary>
/// Supplies a snapshot of a terminal screen buffer so its content can be sampled
/// into a <see cref="SceneTexture2D"/> and projected onto 3D mesh geometry.
/// </summary>
/// <remarks>
/// <para>
/// This is the bridge between the terminal world and the scene world: anything that
/// can be wired up to a <see cref="Hex1bTerminal"/> exposes a cell buffer, and that
/// buffer can be turned into a texture. <see cref="TerminalWidgetHandle"/> implements
/// this interface, so the typical flow is:
/// </para>
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithTerminalWidget(out var handle)
///     .Build();
/// _ = terminal.RunAsync(ct);
///
/// var texture = new TerminalTexture(handle);
/// material.Texture = texture.Update(); // call once per frame
/// </code>
/// </remarks>
public interface ITerminalTextureSource
{
    /// <summary>
    /// Gets an atomic snapshot of the current terminal screen buffer along with its
    /// dimensions. The returned buffer is indexed <c>[row, column]</c>.
    /// </summary>
    (TerminalCell[,] Buffer, int Width, int Height) GetScreenBufferSnapshot();
}
