using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays an embedded terminal session.
/// </summary>
/// <remarks>
/// <para>
/// The TerminalWidget binds to a <see cref="TerminalWidgetHandle"/> which provides
/// the screen buffer from a running terminal session. This allows embedding
/// child terminals within a TUI application.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithTerminalWidget(out var bashHandle)
///     .Build();
/// 
/// _ = terminal.RunAsync(appCt);
/// 
/// ctx.Terminal(bashHandle);
/// </code>
/// </para>
/// </remarks>
public sealed record TerminalWidget(TerminalWidgetHandle Handle) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TerminalNode ?? new TerminalNode();
        
        // Unbind from previous handle if different
        if (node.Handle != null && node.Handle != Handle)
        {
            node.Unbind();
        }
        
        node.Handle = Handle;
        node.SourceWidget = this;
        
        // Bind to the new handle
        node.Bind();
        
        return Task.FromResult<Hex1bNode>(node);
    }
    
    internal override Type GetExpectedNodeType() => typeof(TerminalNode);
}
