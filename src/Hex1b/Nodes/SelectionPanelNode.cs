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
/// <see cref="SnapshotHandler"/> is supplied, the node also registers global
/// bindings (default <c>F7</c> cells, <c>F8</c> block, <c>F9</c> lines,
/// <c>F12</c> full) that call <see cref="SnapshotText(SelectionPanelSnapshotMode)"/>
/// and invoke the handler with the result.
/// </remarks>
public sealed class SelectionPanelNode : Hex1bNode
{
    /// <summary>
    /// The child node wrapped by this panel.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Optional handler invoked with the result of
    /// <see cref="SnapshotText(SelectionPanelSnapshotMode)"/> when any snapshot
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

        // Globals because the SelectionPanel itself isn't focusable and the
        // user's focus typically lives outside its subtree (e.g. a TextBox
        // pinned below the scroll panel). Keeping these on the node — rather
        // than free-floating app bindings — means they only exist when there
        // is actually a panel registered to receive snapshots.
        //
        // Function keys are used (rather than Ctrl+Letter chords) because
        // Windows console and most xterm-style terminals encode Ctrl+Letter
        // as a raw ASCII control byte, dropping the Shift modifier entirely
        // — there is no portable way to distinguish Ctrl+Shift+S from Ctrl+S.
        // F-keys carry full modifier reporting via CSI sequences and are the
        // reliable choice for default global chords.
        //
        // The defaults dodge two well-known F-key conflicts: F6 is the
        // TerminalWidget copy-mode default, and F11 toggles full screen in
        // Windows Terminal (intercepted before the app sees it). F7-F9 plus
        // F12 are reliably free across Windows Terminal, conhost, and the
        // common xterm-derived emulators.
        RegisterSnapshotBinding(bindings, Hex1bKey.F7, SelectionPanelWidget.SnapshotCells, SelectionPanelSnapshotMode.Cells, "Snapshot SelectionPanel content (cell stream)");
        RegisterSnapshotBinding(bindings, Hex1bKey.F8, SelectionPanelWidget.SnapshotBlock, SelectionPanelSnapshotMode.Block, "Snapshot SelectionPanel content (block)");
        RegisterSnapshotBinding(bindings, Hex1bKey.F9, SelectionPanelWidget.SnapshotLines, SelectionPanelSnapshotMode.Lines, "Snapshot SelectionPanel content (lines)");
        RegisterSnapshotBinding(bindings, Hex1bKey.F12, SelectionPanelWidget.Snapshot, SelectionPanelSnapshotMode.Full, "Snapshot SelectionPanel content (full)");
    }

    private void RegisterSnapshotBinding(
        InputBindingsBuilder bindings,
        Hex1bKey key,
        ActionId actionId,
        SelectionPanelSnapshotMode mode,
        string description)
    {
        bindings.Key(key).Global().Triggers(
            actionId,
            async _ =>
            {
                var handler = SnapshotHandler;
                if (handler is not null)
                {
                    await handler(SnapshotText(mode));
                }
            },
            description);
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
    /// Equivalent to calling <see cref="SnapshotText(SelectionPanelSnapshotMode)"/>
    /// with <see cref="SelectionPanelSnapshotMode.Full"/>.
    /// </remarks>
    public string SnapshotText() => SnapshotText(SelectionPanelSnapshotMode.Full);

    /// <summary>
    /// Renders the wrapped subtree into a fresh <see cref="Surface"/> and reads
    /// back the cells corresponding to <paramref name="mode"/>'s selection
    /// geometry as plain text.
    /// </summary>
    /// <remarks>
    /// Until the interactive copy mode is built out, the geometry for non-Full
    /// modes is hard-coded to the middle ~50% of the rendered surface so each
    /// mode produces a visibly distinct slice. Trailing whitespace on each
    /// emitted line is trimmed; wide-character continuation cells
    /// (<see cref="SurfaceCell.IsContinuation"/>) are skipped so wide
    /// characters appear once. If the panel has not been arranged yet (or has
    /// zero-sized bounds), an empty string is returned.
    /// </remarks>
    public string SnapshotText(SelectionPanelSnapshotMode mode)
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

        return mode switch
        {
            SelectionPanelSnapshotMode.Full => ReadFull(surface),
            SelectionPanelSnapshotMode.Cells => ReadCellStream(surface),
            SelectionPanelSnapshotMode.Block => ReadBlock(surface),
            SelectionPanelSnapshotMode.Lines => ReadLines(surface),
            _ => ReadFull(surface),
        };
    }

    private static string ReadFull(Surface surface)
    {
        var sb = new StringBuilder(surface.Width * surface.Height);
        for (int y = 0; y < surface.Height; y++)
        {
            AppendLine(surface, sb, y, fromCol: 0, toColInclusive: surface.Width - 1);
        }
        return TrimTrailingNewlines(sb);
    }

    private static string ReadLines(Surface surface)
    {
        var (topRow, bottomRow, _, _) = ComputeMidGeometry(surface);
        var sb = new StringBuilder();
        for (int y = topRow; y <= bottomRow; y++)
        {
            AppendLine(surface, sb, y, fromCol: 0, toColInclusive: surface.Width - 1);
        }
        return TrimTrailingNewlines(sb);
    }

    private static string ReadBlock(Surface surface)
    {
        var (topRow, bottomRow, leftCol, rightCol) = ComputeMidGeometry(surface);
        var sb = new StringBuilder();
        for (int y = topRow; y <= bottomRow; y++)
        {
            AppendLine(surface, sb, y, fromCol: leftCol, toColInclusive: rightCol);
        }
        return TrimTrailingNewlines(sb);
    }

    private static string ReadCellStream(Surface surface)
    {
        // Mirrors how a terminal mouse drag selects: from a start position
        // (topRow, leftCol) to an end position (bottomRow, rightCol). The
        // start row is taken from the start column to the end of the row,
        // every fully-spanned row is taken in full, and the end row is
        // taken from column zero up to the end column.
        var (topRow, bottomRow, leftCol, rightCol) = ComputeMidGeometry(surface);
        var sb = new StringBuilder();

        if (topRow == bottomRow)
        {
            AppendLine(surface, sb, topRow, fromCol: leftCol, toColInclusive: rightCol);
        }
        else
        {
            AppendLine(surface, sb, topRow, fromCol: leftCol, toColInclusive: surface.Width - 1);
            for (int y = topRow + 1; y < bottomRow; y++)
            {
                AppendLine(surface, sb, y, fromCol: 0, toColInclusive: surface.Width - 1);
            }
            AppendLine(surface, sb, bottomRow, fromCol: 0, toColInclusive: rightCol);
        }

        return TrimTrailingNewlines(sb);
    }

    private static (int topRow, int bottomRow, int leftCol, int rightCol) ComputeMidGeometry(Surface surface)
    {
        // Middle ~50% of the surface, clamped so the range is never inverted
        // and always contains at least one row/column even on tiny surfaces.
        int w = surface.Width;
        int h = surface.Height;

        int topRow = Math.Max(0, h / 4);
        int bottomRow = Math.Max(topRow, Math.Min(h - 1, (h * 3) / 4 - 1));
        int leftCol = Math.Max(0, w / 4);
        int rightCol = Math.Max(leftCol, Math.Min(w - 1, (w * 3) / 4 - 1));
        return (topRow, bottomRow, leftCol, rightCol);
    }

    private static void AppendLine(Surface surface, StringBuilder sb, int y, int fromCol, int toColInclusive)
    {
        int lineStart = sb.Length;
        int trimmedLength = 0;

        for (int x = fromCol; x <= toColInclusive; x++)
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
                    trimmedLength = sb.Length - lineStart;
                }
            }
        }

        // Trim trailing whitespace on this line so unfilled cells don't
        // bloat the output with thousands of spaces.
        sb.Length = lineStart + trimmedLength;
        sb.AppendLine();
    }

    private static string TrimTrailingNewlines(StringBuilder sb)
        => sb.ToString().TrimEnd('\r', '\n');

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
