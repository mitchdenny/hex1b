using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Interface for components that can provide diagnostic tree information.
/// Implemented by Hex1bApp to expose the widget/node tree for MCP diagnostics.
/// </summary>
internal interface IDiagnosticTreeProvider
{
    /// <summary>
    /// Gets the current node tree as a JSON-serializable diagnostic tree.
    /// </summary>
    DiagnosticNode? GetDiagnosticTree();
    
    /// <summary>
    /// Gets the current popup stack as diagnostic entries.
    /// </summary>
    IReadOnlyList<DiagnosticPopupEntry> GetDiagnosticPopups();
    
    /// <summary>
    /// Gets information about the focus ring (focusable nodes and their order).
    /// </summary>
    DiagnosticFocusInfo GetDiagnosticFocusInfo();
}

/// <summary>
/// A diagnostic representation of a node in the UI tree.
/// </summary>
internal sealed class DiagnosticNode
{
    /// <summary>
    /// The type name of the node (e.g., "ButtonNode", "TextBlockNode").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    /// <summary>
    /// The type name of the source widget (e.g., "ButtonWidget").
    /// </summary>
    [JsonPropertyName("widgetType")]
    public string? WidgetType { get; set; }
    
    /// <summary>
    /// The node's bounds after layout.
    /// </summary>
    [JsonPropertyName("bounds")]
    public DiagnosticRect Bounds { get; set; } = new();
    
    /// <summary>
    /// The node's hit test bounds (may differ from Bounds).
    /// </summary>
    [JsonPropertyName("hitTestBounds")]
    public DiagnosticRect HitTestBounds { get; set; } = new();
    
    /// <summary>
    /// The node's content bounds (for AnchoredNode, etc.).
    /// </summary>
    [JsonPropertyName("contentBounds")]
    public DiagnosticRect ContentBounds { get; set; } = new();
    
    /// <summary>
    /// Whether this node is focusable.
    /// </summary>
    [JsonPropertyName("isFocusable")]
    public bool IsFocusable { get; set; }
    
    /// <summary>
    /// Whether this node currently has focus.
    /// </summary>
    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; set; }
    
    /// <summary>
    /// Key properties of the node for debugging.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object?>? Properties { get; set; }
    
    /// <summary>
    /// Child nodes.
    /// </summary>
    [JsonPropertyName("children")]
    public List<DiagnosticNode>? Children { get; set; }
    
    /// <summary>
    /// Creates a diagnostic node from a Hex1b node.
    /// </summary>
    public static DiagnosticNode FromNode(Hex1bNode node)
    {
        var diagNode = new DiagnosticNode
        {
            Type = node.GetType().Name,
            Bounds = DiagnosticRect.FromRect(node.Bounds),
            HitTestBounds = DiagnosticRect.FromRect(node.HitTestBounds),
            ContentBounds = DiagnosticRect.FromRect(node.ContentBounds),
            IsFocusable = node.IsFocusable,
            IsFocused = node.IsFocused,
            Properties = GetNodeProperties(node)
        };
        
        // Add children
        var children = node.GetChildren().ToList();
        if (children.Count > 0)
        {
            diagNode.Children = children.Select(FromNode).ToList();
        }
        
        return diagNode;
    }
    
    private static Dictionary<string, object?>? GetNodeProperties(Hex1bNode node)
    {
        var props = new Dictionary<string, object?>();
        
        // Add type-specific properties for common node types
        switch (node)
        {
            case ButtonNode button:
                props["label"] = button.Label;
                break;
            case TextBlockNode textBlock:
                props["text"] = textBlock.Text?.Length > 50 
                    ? textBlock.Text[..50] + "..." 
                    : textBlock.Text;
                break;
            case ListNode list:
                props["itemCount"] = list.Items?.Count ?? 0;
                props["selectedIndex"] = list.SelectedIndex;
                break;
            case PickerNode picker:
                props["selectedIndex"] = picker.SelectedIndex;
                props["selectedText"] = picker.SelectedText;
                break;
            case MenuItemNode menuItem:
                props["label"] = menuItem.Label;
                break;
            case AnchoredNode anchored:
                props["anchorNodeType"] = anchored.AnchorNode?.GetType().Name;
                props["anchorBounds"] = anchored.AnchorNode != null 
                    ? DiagnosticRect.FromRect(anchored.AnchorNode.Bounds) 
                    : null;
                props["isAnchorStale"] = anchored.IsAnchorStale;
                props["position"] = anchored.Position.ToString();
                break;
            case BackdropNode backdrop:
                props["style"] = backdrop.Style.ToString();
                props["hasClickAwayHandler"] = backdrop.ClickAwayHandler != null || backdrop.ClickAwayEventHandler != null;
                break;
            case NotificationPanelNode notificationPanel:
                props["isDrawerExpanded"] = notificationPanel.IsDrawerExpanded;
                props["notificationCount"] = notificationPanel.Notifications?.Count ?? 0;
                break;
        }
        
        return props.Count > 0 ? props : null;
    }
}

/// <summary>
/// A diagnostic representation of a rectangle.
/// </summary>
internal sealed class DiagnosticRect
{
    [JsonPropertyName("x")]
    public int X { get; set; }
    
    [JsonPropertyName("y")]
    public int Y { get; set; }
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    public static DiagnosticRect FromRect(Rect rect) => new()
    {
        X = rect.X,
        Y = rect.Y,
        Width = rect.Width,
        Height = rect.Height
    };
    
    public override string ToString() => $"({X},{Y},{Width},{Height})";
}

/// <summary>
/// Diagnostic information about a popup entry.
/// </summary>
internal sealed class DiagnosticPopupEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "";
    
    [JsonPropertyName("hasBackdrop")]
    public bool HasBackdrop { get; set; }
    
    [JsonPropertyName("isAnchored")]
    public bool IsAnchored { get; set; }
    
    [JsonPropertyName("isBarrier")]
    public bool IsBarrier { get; set; }
    
    [JsonPropertyName("contentBounds")]
    public DiagnosticRect? ContentBounds { get; set; }
    
    [JsonPropertyName("anchorInfo")]
    public DiagnosticAnchorInfo? AnchorInfo { get; set; }
    
    [JsonPropertyName("focusRestoreNodeType")]
    public string? FocusRestoreNodeType { get; set; }
}

/// <summary>
/// Diagnostic information about an anchor.
/// </summary>
internal sealed class DiagnosticAnchorInfo
{
    [JsonPropertyName("anchorNodeType")]
    public string? AnchorNodeType { get; set; }
    
    [JsonPropertyName("anchorBounds")]
    public DiagnosticRect? AnchorBounds { get; set; }
    
    [JsonPropertyName("isStale")]
    public bool IsStale { get; set; }
    
    [JsonPropertyName("position")]
    public string? Position { get; set; }
}

/// <summary>
/// Diagnostic information about the focus ring.
/// </summary>
internal sealed class DiagnosticFocusInfo
{
    [JsonPropertyName("focusableCount")]
    public int FocusableCount { get; set; }
    
    [JsonPropertyName("currentFocusIndex")]
    public int CurrentFocusIndex { get; set; }
    
    [JsonPropertyName("focusedNodeType")]
    public string? FocusedNodeType { get; set; }
    
    [JsonPropertyName("focusables")]
    public List<DiagnosticFocusableEntry>? Focusables { get; set; }
    
    [JsonPropertyName("lastHitTestDebug")]
    public string? LastHitTestDebug { get; set; }
}

/// <summary>
/// A focusable entry in the focus ring.
/// </summary>
internal sealed class DiagnosticFocusableEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("bounds")]
    public DiagnosticRect Bounds { get; set; } = new();
    
    [JsonPropertyName("hitTestBounds")]
    public DiagnosticRect HitTestBounds { get; set; } = new();
    
    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; set; }
}
