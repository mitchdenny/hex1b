using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

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
    
    public override string ToString() => $"x={X} y={Y} w={Width} h={Height} ({X},{Y} → {X + Width},{Y + Height})";
}
