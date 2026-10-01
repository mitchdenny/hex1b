using System.Diagnostics;
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
    
    /// <summary>
    /// Gets frame-level performance metrics for the last rendered frame.
    /// </summary>
    DiagnosticFrameInfo GetDiagnosticFrameInfo();
}
