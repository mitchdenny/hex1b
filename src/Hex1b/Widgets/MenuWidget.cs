using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A menu that can contain menu items, separators, and submenus.
/// When used in a MenuBar, renders as a clickable trigger.
/// When used in another Menu, renders as a submenu item with an arrow indicator.
/// </summary>
/// <param name="Label">The display label for the menu.</param>
/// <param name="Children">The menu's children (items, separators, submenus).</param>
public sealed record MenuWidget(string Label, IReadOnlyList<IMenuChild> Children) : Hex1bWidget, IMenuChild
{
    /// <summary>
    /// The explicitly specified accelerator character (from &amp; syntax).
    /// </summary>
    internal char? ExplicitAccelerator { get; init; }
    
    /// <summary>
    /// The index of the accelerator character in the display label.
    /// </summary>
    internal int AcceleratorIndex { get; init; } = -1;
    
    /// <summary>
    /// Whether to disable automatic accelerator assignment.
    /// </summary>
    internal bool DisableAccelerator { get; init; }
    
    /// <summary>
    /// Disables automatic accelerator assignment for this menu.
    /// </summary>
    public MenuWidget NoAccelerator() => this with { DisableAccelerator = true };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuNode ?? new MenuNode();
        
        // Mark dirty if label changed
        if (node.Label != Label)
        {
            node.MarkDirty();
        }
        
        // Compute automatic accelerators for children
        var usedAccelerators = new HashSet<char>();
        var childAccelerators = new List<(IMenuChild Child, char? Accelerator, int Index)>();
        
        foreach (var child in Children)
        {
            switch (child)
            {
                case MenuWidget submenu:
                    ProcessChildAccelerator(submenu.Label, submenu.ExplicitAccelerator, submenu.AcceleratorIndex, submenu.DisableAccelerator, submenu, usedAccelerators, childAccelerators);
                    break;
                    
                case MenuItemWidget item:
                    ProcessChildAccelerator(item.Label, item.ExplicitAccelerator, item.AcceleratorIndex, item.DisableAccelerator, item, usedAccelerators, childAccelerators);
                    break;
                    
                case MenuSeparatorWidget separator:
                    childAccelerators.Add((separator, null, -1));
                    break;
            }
        }
        
        node.Label = Label;
        node.Children = Children;
        node.ChildAccelerators = childAccelerators;
        node.SourceWidget = this;
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuNode);
    
    private static void ProcessChildAccelerator(
        string label,
        char? explicitAccelerator,
        int explicitIndex,
        bool disableAccelerator,
        IMenuChild child,
        HashSet<char> usedAccelerators,
        List<(IMenuChild Child, char? Accelerator, int Index)> childAccelerators)
    {
        if (disableAccelerator)
        {
            childAccelerators.Add((child, null, -1));
            return;
        }
        
        if (explicitAccelerator.HasValue)
        {
            usedAccelerators.Add(explicitAccelerator.Value);
            childAccelerators.Add((child, explicitAccelerator, explicitIndex));
            return;
        }
        
        // Auto-assign accelerator
        var (accel, index) = FindAutoAccelerator(label, usedAccelerators);
        if (accel.HasValue)
        {
            usedAccelerators.Add(accel.Value);
        }
        childAccelerators.Add((child, accel, index));
    }
    
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
