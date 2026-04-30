namespace CloudTermDemo;

/// <summary>
/// Tracks the user's current position in the cloud resource hierarchy
/// and provides navigation operations for the cloud shell.
/// </summary>
public sealed class CloudShellState
{
    private readonly CloudNode _root;

    public CloudShellState(CloudNode root)
    {
        _root = root;
        CurrentNode = root;
    }

    public CloudNode CurrentNode { get; private set; }

    /// <summary>
    /// Returns the breadcrumb path from root to the current node.
    /// </summary>
    public string GetPath()
    {
        var parts = new List<string>();
        var node = CurrentNode;
        while (node != null)
        {
            parts.Add(node.Name);
            node = node.Parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>
    /// Returns a short prompt-friendly path (last 2-3 segments).
    /// </summary>
    public string GetPrompt()
    {
        var parts = new List<string>();
        var node = CurrentNode;
        for (var i = 0; i < 3 && node != null; i++)
        {
            parts.Add(node.Name);
            node = node.Parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>
    /// Navigate into a child by name. Returns true if found.
    /// </summary>
    public bool NavigateTo(string name)
    {
        var child = CurrentNode.Children
            .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (child != null)
        {
            CurrentNode = child;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Navigate up to parent. Returns true if not already at root.
    /// </summary>
    public bool NavigateUp()
    {
        if (CurrentNode.Parent != null)
        {
            CurrentNode = CurrentNode.Parent;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Navigate to root.
    /// </summary>
    public void NavigateToRoot()
    {
        CurrentNode = _root;
    }
}
