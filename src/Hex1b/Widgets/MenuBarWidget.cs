using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A horizontal menu bar that contains top-level menus.
/// Typically placed at the top of an application.
/// </summary>
/// <param name="Menus">The top-level menus in the bar.</param>
public sealed record MenuBarWidget(IReadOnlyList<MenuWidget> Menus) : Hex1bWidget
{
    /// <summary>Rebindable action: Focus previous menu.</summary>
    public static readonly ActionId PreviousMenu = new($"{nameof(MenuBarWidget)}.{nameof(PreviousMenu)}");
    /// <summary>Rebindable action: Focus next menu.</summary>
    public static readonly ActionId NextMenu = new($"{nameof(MenuBarWidget)}.{nameof(NextMenu)}");
    /// <summary>Rebindable action: Focus next focusable element.</summary>
    public static readonly ActionId NextFocusable = new($"{nameof(MenuBarWidget)}.{nameof(NextFocusable)}");
    /// <summary>Rebindable action: Focus previous focusable element.</summary>
    public static readonly ActionId PreviousFocusable = new($"{nameof(MenuBarWidget)}.{nameof(PreviousFocusable)}");

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuBarNode ?? new MenuBarNode();
        
        // Compute automatic accelerators for menus that don't have explicit ones
        var usedAccelerators = new HashSet<char>();
        var menuAccelerators = new List<(MenuWidget Menu, char? Accelerator, int Index)>();
        
        foreach (var menu in Menus)
        {
            if (menu.DisableAccelerator)
            {
                menuAccelerators.Add((menu, null, -1));
                continue;
            }
            
            if (menu.ExplicitAccelerator.HasValue)
            {
                usedAccelerators.Add(menu.ExplicitAccelerator.Value);
                menuAccelerators.Add((menu, menu.ExplicitAccelerator, menu.AcceleratorIndex));
                continue;
            }
            
            // Auto-assign accelerator
            var (accel, index) = FindAutoAccelerator(menu.Label, usedAccelerators);
            if (accel.HasValue)
            {
                usedAccelerators.Add(accel.Value);
            }
            menuAccelerators.Add((menu, accel, index));
        }
        
        node.Menus = Menus;
        node.MenuAccelerators = menuAccelerators;
        node.SourceWidget = this;
        node.MarkDirty();
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuBarNode);
    
    /// <summary>
    /// Finds an automatic accelerator for a label.
    /// </summary>
    private static (char? Accelerator, int Index) FindAutoAccelerator(string label, HashSet<char> usedAccelerators)
    {
        for (var i = 0; i < label.Length; i++)
        {
            var c = label[i];
            if (char.IsLetterOrDigit(c))
            {
                var upper = char.ToUpperInvariant(c);
                if (!usedAccelerators.Contains(upper))
                {
                    return (upper, i);
                }
            }
        }
        return (null, -1);
    }
}
