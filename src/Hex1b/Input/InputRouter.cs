namespace Hex1b.Input;

/// <summary>
/// Routes input through the node tree to the focused node, collecting and resolving bindings.
/// </summary>
public static class InputRouter
{
    /// <summary>
    /// Routes a key event through the node tree using the new explicit routing model.
    /// 
    /// Algorithm:
    /// 1. Build path from root to focused node
    /// 2. Walk DOWN the path, collecting bindings at each level (child bindings override parent)
    /// 3. Try to execute a matching binding
    /// 4. If no binding matched, let the focused node handle the input directly
    /// 5. If focused node doesn't handle it, bubble UP to container nodes (for Tab navigation, etc.)
    /// </summary>
    public static InputResult RouteInput(Hex1bNode root, Hex1bKeyEvent keyEvent)
    {
        // Build path from root to focused node
        var path = BuildPathToFocused(root);
        if (path.Count == 0)
        {
            // No focused node found, nothing to route to
            return InputResult.NotHandled;
        }

        // Create input context
        var context = new InputContext(keyEvent);

        // Walk down the path, collecting bindings (child overrides parent)
        foreach (var node in path)
        {
            context.AddBindings(node.InputBindings);
        }

        // Try to execute a matching binding
        if (context.TryExecuteBinding())
        {
            return InputResult.Handled;
        }

        // No binding matched, let the focused node handle the input directly
        var focusedNode = path[^1];
        var result = focusedNode.HandleInput(keyEvent);
        if (result == InputResult.Handled)
        {
            return result;
        }

        // Focused node didn't handle it, bubble UP to container nodes
        // Walk from the parent of focused node back to root
        for (int i = path.Count - 2; i >= 0; i--)
        {
            result = path[i].HandleInput(keyEvent);
            if (result == InputResult.Handled)
            {
                return result;
            }
        }

        return InputResult.NotHandled;
    }

    /// <summary>
    /// Builds a path from the root node to the currently focused node.
    /// Returns empty list if no focused node is found.
    /// </summary>
    private static List<Hex1bNode> BuildPathToFocused(Hex1bNode root)
    {
        var path = new List<Hex1bNode>();
        BuildPathRecursive(root, path);
        return path;
    }

    private static bool BuildPathRecursive(Hex1bNode node, List<Hex1bNode> path)
    {
        path.Add(node);

        // If this node is focusable and focused, we found our target
        if (node.IsFocusable && node.IsFocused)
        {
            return true;
        }

        // Check children
        foreach (var child in node.GetChildren())
        {
            if (BuildPathRecursive(child, path))
            {
                return true;
            }
        }

        // No focused node found in this subtree, backtrack
        path.RemoveAt(path.Count - 1);
        return false;
    }
}
