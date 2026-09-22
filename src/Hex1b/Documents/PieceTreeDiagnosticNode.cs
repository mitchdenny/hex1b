using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// Diagnostic snapshot of a piece tree node for visualization.
/// </summary>
public sealed class PieceTreeDiagnosticNode
{
    public string Source { get; init; } = "";
    public int Start { get; init; }
    public int Length { get; init; }
    public int LeftSubtreeSize { get; init; }
    public string Color { get; init; } = "";
    public PieceTreeDiagnosticNode? Left { get; init; }
    public PieceTreeDiagnosticNode? Right { get; init; }
}
