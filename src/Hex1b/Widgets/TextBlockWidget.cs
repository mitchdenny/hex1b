namespace Hex1b.Widgets;

/// <summary>
/// Displays text content in the terminal. This is the primary widget for rendering
/// static or dynamic text strings.
/// </summary>
/// <param name="Text">The text content to display.</param>
/// <param name="Overflow">
/// Controls how text handles horizontal overflow when it exceeds the available width.
/// Defaults to <see cref="TextOverflow.Truncate"/>.
/// </param>
/// <remarks>
/// <para>
/// TextBlockWidget is a read-only text display widget. For editable text input,
/// use <see cref="TextBoxWidget"/> instead.
/// </para>
/// <para>
/// The widget supports three overflow behaviors:
/// <list type="bullet">
/// <item><description><see cref="TextOverflow.Truncate"/>: Text is clipped by parent (no visual indicator)</description></item>
/// <item><description><see cref="TextOverflow.Wrap"/>: Text wraps to multiple lines at word boundaries</description></item>
/// <item><description><see cref="TextOverflow.Ellipsis"/>: Text is truncated with "..." when it exceeds width</description></item>
/// </list>
/// </para>
/// <para>
/// TextBlockWidget correctly handles Unicode text including wide characters (CJK),
/// combining characters, and emoji.
/// </para>
/// </remarks>
/// <example>
/// <para>Basic text display:</para>
/// <code>
/// ctx.Text("Hello, World!")
/// </code>
/// <para>Text with wrapping:</para>
/// <code>
/// ctx.Text("This long text will wrap to multiple lines").Wrap()
/// </code>
/// <para>Text with ellipsis truncation:</para>
/// <code>
/// ctx.Text("Very long text that gets truncated...").Ellipsis()
/// </code>
/// </example>
/// <seealso cref="TextOverflow"/>
/// <seealso cref="TextBoxWidget"/>
public sealed record TextBlockWidget(string Text, TextOverflow Overflow = TextOverflow.Truncate) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBlockNode ?? new TextBlockNode();
        
        // Mark dirty if properties changed
        if (node.Text != Text || node.Overflow != Overflow)
        {
            node.MarkDirty();
        }
        
        node.Text = Text;
        node.Overflow = Overflow;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TextBlockNode);
}
