namespace Hex1b.Widgets;

/// <summary>
/// Specifies the style of guide lines used to draw tree hierarchy.
/// </summary>
public enum TreeGuideStyle
{
    /// <summary>
    /// Unicode box-drawing characters (├─, └─, │).
    /// </summary>
    Unicode,
    
    /// <summary>
    /// ASCII characters (+-, \-, |).
    /// </summary>
    Ascii,
    
    /// <summary>
    /// Bold Unicode box-drawing characters (┣━, ┗━, ┃).
    /// </summary>
    Bold,
    
    /// <summary>
    /// Double-line Unicode box-drawing characters (╠═, ╚═, ║).
    /// </summary>
    Double
}
