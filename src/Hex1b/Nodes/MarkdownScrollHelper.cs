using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Adds scroll bindings (keyboard and mouse wheel) to focusable markdown
/// nodes (headings and link regions) so that the user can scroll the
/// containing <see cref="ScrollPanelNode"/> while a child element has focus.
/// </summary>
internal static class MarkdownScrollHelper
{
    internal static void AddScrollBindings(Hex1bNode node, InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.UpArrow)
            .Triggers(ScrollPanelWidget.ScrollUpAction, _ => Scroll(node, -1), "Scroll up");
        bindings.Key(Hex1bKey.DownArrow)
            .Triggers(ScrollPanelWidget.ScrollDownAction, _ => Scroll(node, 1), "Scroll down");
        bindings.Key(Hex1bKey.PageUp)
            .Triggers(ScrollPanelWidget.PageUpAction, _ => ScrollPage(node, -1), "Page up");
        bindings.Key(Hex1bKey.PageDown)
            .Triggers(ScrollPanelWidget.PageDownAction, _ => ScrollPage(node, 1), "Page down");
        bindings.Key(Hex1bKey.Home)
            .Triggers(ScrollPanelWidget.ScrollToStartAction, _ => ScrollTo(node, 0), "Scroll to top");
        bindings.Key(Hex1bKey.End)
            .Triggers(ScrollPanelWidget.ScrollToEndAction, _ => ScrollToEnd(node), "Scroll to bottom");

        bindings.Mouse(MouseButton.ScrollUp)
            .Triggers(ScrollPanelWidget.MouseScrollUpAction, _ => Scroll(node, -3), "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown)
            .Triggers(ScrollPanelWidget.MouseScrollDownAction, _ => Scroll(node, 3), "Scroll down");
    }

    private static ScrollPanelNode? FindAncestorScrollPanel(Hex1bNode node)
    {
        for (var ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor is ScrollPanelNode sp)
                return sp;
        }

        return null;
    }

    private static Task Scroll(Hex1bNode node, int lines)
    {
        var sp = FindAncestorScrollPanel(node);
        if (sp != null)
        {
            sp.SuppressEnsureFocusedVisibleFor = node;
            sp.SetOffset(sp.Offset + lines);
        }

        return Task.CompletedTask;
    }

    private static Task ScrollPage(Hex1bNode node, int direction)
    {
        var sp = FindAncestorScrollPanel(node);
        if (sp != null)
        {
            sp.SuppressEnsureFocusedVisibleFor = node;
            sp.SetOffset(sp.Offset + (sp.ViewportSize - 1) * direction);
        }

        return Task.CompletedTask;
    }

    private static Task ScrollTo(Hex1bNode node, int offset)
    {
        var sp = FindAncestorScrollPanel(node);
        if (sp != null)
        {
            sp.SuppressEnsureFocusedVisibleFor = node;
            sp.SetOffset(offset);
        }

        return Task.CompletedTask;
    }

    private static Task ScrollToEnd(Hex1bNode node)
    {
        var sp = FindAncestorScrollPanel(node);
        if (sp != null)
        {
            sp.SuppressEnsureFocusedVisibleFor = node;
            sp.SetOffset(sp.MaxOffset);
        }

        return Task.CompletedTask;
    }
}
