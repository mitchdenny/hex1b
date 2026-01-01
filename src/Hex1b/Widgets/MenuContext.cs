namespace Hex1b.Widgets;

/// <summary>
/// Provides a fluent API context for building menu structures.
/// This context exposes only menu-related methods (Menu, MenuItem, Separator)
/// to guide developers toward the correct API usage.
/// </summary>
public readonly struct MenuContext
{
    /// <summary>
    /// Creates a submenu with the specified label and children.
    /// </summary>
    /// <param name="label">The menu label. Use &amp; before a character to specify an explicit accelerator (e.g., "&amp;File").</param>
    /// <param name="children">A function that returns the menu children.</param>
    /// <returns>A MenuWidget configured with the specified label and children.</returns>
    public MenuWidget Menu(string label, Func<MenuContext, IEnumerable<IMenuChild>> children)
    {
        var ctx = new MenuContext();
        var childList = children(ctx).ToList();
        var (displayLabel, accelerator, acceleratorIndex) = ParseAccelerator(label);
        return new MenuWidget(displayLabel, childList)
        {
            ExplicitAccelerator = accelerator,
            AcceleratorIndex = acceleratorIndex
        };
    }

    /// <summary>
    /// Creates a menu item with the specified label.
    /// </summary>
    /// <param name="label">The item label. Use &amp; before a character to specify an explicit accelerator (e.g., "&amp;Open").</param>
    /// <returns>A MenuItemWidget configured with the specified label.</returns>
    public MenuItemWidget MenuItem(string label)
    {
        var (displayLabel, accelerator, acceleratorIndex) = ParseAccelerator(label);
        return new MenuItemWidget(displayLabel)
        {
            ExplicitAccelerator = accelerator,
            AcceleratorIndex = acceleratorIndex
        };
    }

    /// <summary>
    /// Creates a menu separator.
    /// </summary>
    /// <returns>A MenuSeparatorWidget.</returns>
    public MenuSeparatorWidget Separator() => new();

    /// <summary>
    /// Parses a label for explicit accelerator markers (&amp;).
    /// </summary>
    /// <param name="label">The label potentially containing &amp; markers.</param>
    /// <returns>A tuple of (display label without &amp;, accelerator char if explicit, index in display label).</returns>
    internal static (string DisplayLabel, char? Accelerator, int AcceleratorIndex) ParseAccelerator(string label)
    {
        // Build display label and find accelerator in a single pass
        var displayBuilder = new System.Text.StringBuilder();
        char? accelerator = null;
        int acceleratorIndex = -1;
        
        for (int i = 0; i < label.Length; i++)
        {
            var c = label[i];
            
            if (c == '&' && i + 1 < label.Length)
            {
                var next = label[i + 1];
                
                if (next == '&')
                {
                    // Escaped ampersand - render as literal &
                    displayBuilder.Append('&');
                    i++; // Skip the second &
                }
                else if (accelerator == null)
                {
                    // First accelerator marker
                    accelerator = char.ToUpperInvariant(next);
                    acceleratorIndex = displayBuilder.Length;
                    displayBuilder.Append(next);
                    i++; // Skip the next char (it's the accelerator)
                }
                else
                {
                    // Already have an accelerator, treat & as literal
                    displayBuilder.Append(c);
                }
            }
            else if (c == '&')
            {
                // & at end of string, ignore it
            }
            else
            {
                displayBuilder.Append(c);
            }
        }
        
        return (displayBuilder.ToString(), accelerator, acceleratorIndex);
    }
}
