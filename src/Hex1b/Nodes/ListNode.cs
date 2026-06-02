using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// String-specialized list node. Inherits all selection/scrolling/template
/// behavior from <see cref="TypedListNode{T}"/> with <c>T = string</c>; exists
/// as a distinct concrete type so legacy <see cref="ListWidget"/> handlers and
/// tests that reference <c>ListNode</c> continue to compile and run unchanged.
/// </summary>
public sealed class ListNode : TypedListNode<string>
{
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Redirect default bindings to the legacy ListWidget action ids so existing
        // rebind code (b.Remove(ListWidget.MoveUp), etc.) keeps working.
        ConfigureDefaultBindings(
            bindings,
            ListWidget.MoveUp,
            ListWidget.MoveDown,
            ListWidget.Activate,
            ListWidget.ScrollUp,
            ListWidget.ScrollDown);
    }
}
