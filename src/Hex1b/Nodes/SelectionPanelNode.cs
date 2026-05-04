using System.Text;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="SelectionPanelWidget"/>.
/// </summary>
/// <remarks>
/// Layout, focus, and input remain delegated to the wrapped child. When a
/// <see cref="SnapshotHandler"/> is supplied, the node also registers a global
/// binding (default <c>F12</c>, action id
/// <see cref="SelectionPanelWidget.Snapshot"/>) that calls
/// <see cref="SnapshotText"/> and invokes the handler with the result.
/// </remarks>
public sealed class SelectionPanelNode : Hex1bNode
{
    /// <summary>
    /// The child node wrapped by this panel.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Optional handler invoked with <see cref="SnapshotText"/> when the snapshot
    /// action fires. When <c>null</c>, no binding is registered.
    /// </summary>
    public Func<string, Task>? SnapshotHandler { get; set; }

    public override bool IsFocusable => false;

    public override bool IsFocused
    {
        get => false;
        set
        {
            if (Child != null)
                Child.IsFocused = value;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (SnapshotHandler is null)
        {
            return;
        }

        // Global because the SelectionPanel itself isn't focusable and the
        // user's focus typically lives outside its subtree (e.g. a TextBox
        // pinned below the scroll panel). Keeping this on the node — rather
        // than a free-floating app binding — means the binding only exists
        // when there is actually a panel registered to receive the snapshot.
        //
        // F12 is used (rather than a Ctrl+Letter chord) because Windows
        // console and most xterm-style terminals encode Ctrl+Letter as a raw
        // ASCII control byte, dropping the Shift modifier entirely — there
        // is no portable way to distinguish Ctrl+Shift+S from Ctrl+S. Function
        // keys carry full modifier reporting via CSI sequences, so they are
        // the reliable choice for a default key.
        bindings.Key(Hex1bKey.F12).Global().Triggers(
            SelectionPanelWidget.Snapshot,
            async _ =>
            {
                var handler = SnapshotHandler;
                if (handler is not null)
                {
                    await handler(SnapshotText());
                }
            },
            "Snapshot SelectionPanel content");
    }

    /// <summary>
    /// Renders the wrapped subtree into a fresh <see cref="Surface"/> sized to
    /// the child's arranged bounds and reads back the cells row-by-row as
    /// plain text. The returned string therefore reproduces what the user
    /// sees on screen — including box-drawing border characters — for the
    /// full content of the panel (not just the portion currently visible
    /// inside any enclosing scroll viewport).
    /// </summary>
    /// <remarks>
    /// Trailing whitespace on each line is trimmed. Wide-character
    /// continuation cells (<see cref="SurfaceCell.IsContinuation"/>) are
    /// skipped so wide characters appear once. If the panel hasn't been
    /// arranged yet (or has zero-sized bounds), an empty string is returned.
    /// </remarks>
    public string SnapshotText()
    {
        if (Child is null)
        {
            return string.Empty;
        }

        var bounds = Child.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return string.Empty;
        }

        // Render the child into a private surface sized to its full arranged
        // bounds. The offset constructor translates absolute coordinates
        // (bounds.X, bounds.Y) into surface-local (0, 0). When the child sits
        // inside a ScrollPanel, its arranged Bounds spans the entire content,
        // so the snapshot includes everything — even rows scrolled out of
        // view in the live UI.
        var surface = new Surface(bounds.Width, bounds.Height);
        var context = new SurfaceRenderContext(surface, bounds.X, bounds.Y);
        Child.Render(context);

        var sb = new StringBuilder(bounds.Width * bounds.Height);
        for (int y = 0; y < bounds.Height; y++)
        {
            int trimmedLineLength = 0;
            int lineStart = sb.Length;
            for (int x = 0; x < bounds.Width; x++)
            {
                if (!surface.TryGetCell(x, y, out var cell))
                {
                    sb.Append(' ');
                    continue;
                }

                if (cell.IsContinuation)
                {
                    // The wide character in a previous cell has already
                    // contributed its grapheme; skip the placeholder.
                    continue;
                }

                var character = cell.Character;
                if (string.IsNullOrEmpty(character))
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(character);
                    if (!IsAllWhitespace(character))
                    {
                        trimmedLineLength = sb.Length - lineStart;
                    }
                }
            }

            // Trim trailing whitespace on this line so unfilled cells don't
            // bloat the output with thousands of spaces.
            sb.Length = lineStart + trimmedLineLength;
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static bool IsAllWhitespace(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsWhiteSpace(s[i]))
            {
                return false;
            }
        }
        return true;
    }
}
