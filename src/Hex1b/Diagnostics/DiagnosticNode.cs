using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

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
    public Dictionary<string, JsonElement>? Properties { get; set; }
    
    /// <summary>
    /// Child nodes.
    /// </summary>
    [JsonPropertyName("children")]
    public List<DiagnosticNode>? Children { get; set; }
    
    /// <summary>
    /// Performance timing for this node (null when diagnostic timing is disabled).
    /// </summary>
    [JsonPropertyName("timing")]
    public DiagnosticTiming? Timing { get; set; }
    
    /// <summary>
    /// Creates a diagnostic node from a Hex1b node.
    /// </summary>
    public static DiagnosticNode FromNode(Hex1bNode node)
    {
        var now = Stopwatch.GetTimestamp();
        var diagNode = new DiagnosticNode
        {
            Type = node.GetType().Name,
            Bounds = DiagnosticRect.FromRect(node.Bounds),
            HitTestBounds = DiagnosticRect.FromRect(node.HitTestBounds),
            ContentBounds = DiagnosticRect.FromRect(node.ContentBounds),
            IsFocusable = node.IsFocusable,
            IsFocused = node.IsFocused,
            Properties = GetNodeProperties(node),
            Timing = node.DiagReconcileTicks > 0 || node.DiagRenderTicks > 0 || node.DiagLastRenderedTimestamp > 0
                ? DiagnosticTiming.FromNode(node, now)
                : null
        };
        
        // Add children
        var children = node.GetChildren().ToList();
        if (children.Count > 0)
        {
            diagNode.Children = children.Select(FromNode).ToList();
        }
        
        return diagNode;
    }
    
    private static Dictionary<string, JsonElement>? GetNodeProperties(Hex1bNode node)
    {
        var props = new Dictionary<string, JsonElement>();
        
        // Add type-specific properties for common node types
        switch (node)
        {
            case ButtonNode button:
                props["label"] = ToElement(button.Label);
                break;
            case TextBlockNode textBlock:
                props["text"] = ToElement(textBlock.Text?.Length > 50 
                    ? textBlock.Text[..50] + "..." 
                    : textBlock.Text);
                break;
            case ListNode list:
                props["itemCount"] = ToElement(list.Items?.Count ?? 0);
                props["selectedIndex"] = ToElement(list.FocusedIndex);
                break;
            case PickerNode picker:
                props["selectedIndex"] = ToElement(picker.SelectedIndex);
                props["selectedText"] = ToElement(picker.SelectedText);
                break;
            case MenuItemNode menuItem:
                props["label"] = ToElement(menuItem.Label);
                break;
            case AnchoredNode anchored:
                props["anchorNodeType"] = ToElement(anchored.AnchorNode?.GetType().Name);
                props["anchorBounds"] = anchored.AnchorNode != null 
                    ? JsonSerializer.SerializeToElement(DiagnosticRect.FromRect(anchored.AnchorNode.Bounds), DiagnosticsJsonContext.Default.DiagnosticRect)
                    : default;
                props["isAnchorStale"] = ToElement(anchored.IsAnchorStale);
                props["position"] = ToElement(anchored.Position.ToString());
                break;
            case BackdropNode backdrop:
                props["style"] = ToElement(backdrop.Style.ToString());
                props["hasClickAwayHandler"] = ToElement(backdrop.ClickAwayHandler != null || backdrop.ClickAwayEventHandler != null);
                break;
            case NotificationPanelNode notificationPanel:
                props["isDrawerExpanded"] = ToElement(notificationPanel.IsDrawerExpanded);
                props["notificationCount"] = ToElement(notificationPanel.Notifications?.Count ?? 0);
                break;
        }
        
        return props.Count > 0 ? props : null;
    }

    private static JsonElement ToElement(string? value)
    {
        return value is null
            ? default
            : JsonSerializer.SerializeToElement(value, DiagnosticsJsonContext.Default.String);
    }

    private static JsonElement ToElement(int value)
    {
        return JsonSerializer.SerializeToElement(value, DiagnosticsJsonContext.Default.Int32);
    }

    private static JsonElement ToElement(bool value)
    {
        return JsonSerializer.SerializeToElement(value, DiagnosticsJsonContext.Default.Boolean);
    }
}
