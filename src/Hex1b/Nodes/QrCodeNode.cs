using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using QRCoder;
using System.Collections;

namespace Hex1b;

/// <summary>
/// Render node for displaying QR codes in the terminal. Created by reconciling a <see cref="QrCodeWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// QrCodeNode handles generating, measuring, arranging, and rendering QR codes using
/// Unicode block characters (█). The QR code is generated using the QRCoder library
/// and rendered to the terminal as a grid of blocks.
/// </para>
/// <para>
/// This node is not focusable and does not handle input.
/// </para>
/// </remarks>
/// <seealso cref="QrCodeWidget"/>
public sealed class QrCodeNode : Hex1bNode
{
    private const string FilledBlock = "██";
    private const string EmptySpace = "  ";
    
    private string _data = "";
    private List<BitArray>? _matrix;
    private int _matrixSize = 0;
    
    /// <summary>
    /// Gets or sets the data to encode in the QR code.
    /// </summary>
    /// <remarks>
    /// When this property changes, the node is marked dirty and the QR code is regenerated.
    /// </remarks>
    public string Data 
    { 
        get => _data; 
        set 
        {
            if (_data != value)
            {
                _data = value;
                _matrix = null; // Invalidate cached matrix
                MarkDirty();
            }
        }
    }
    
    private int _quietZone = 1;
    
    /// <summary>
    /// Gets or sets the number of module widths to use as a border (quiet zone).
    /// </summary>
    /// <remarks>
    /// The quiet zone is the white border around the QR code. A value of 0 disables it.
    /// When this property changes, the node is marked dirty.
    /// </remarks>
    public int QuietZone 
    { 
        get => _quietZone; 
        set 
        {
            if (_quietZone != value)
            {
                _quietZone = Math.Max(0, value);
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Generates the QR code matrix if not already cached.
    /// </summary>
    private void EnsureMatrix()
    {
        if (_matrix != null)
            return;

        if (string.IsNullOrEmpty(_data))
        {
            _matrix = new List<BitArray>();
            _matrixSize = 0;
            return;
        }

        try
        {
            using var generator = new QRCodeGenerator();
            using var qrCodeData = generator.CreateQrCode(_data, QRCodeGenerator.ECCLevel.Q);
            _matrix = qrCodeData.ModuleMatrix;
            _matrixSize = _matrix.Count;
        }
        catch
        {
            // If QR generation fails (e.g., data too long), use empty matrix
            _matrix = new List<BitArray>();
            _matrixSize = 0;
        }
    }

    /// <summary>
    /// Measures the size required to display the QR code within the given constraints.
    /// </summary>
    /// <param name="constraints">The size constraints for layout.</param>
    /// <returns>
    /// The measured size. Width and height include the QR code matrix plus quiet zone borders.
    /// </returns>
    /// <remarks>
    /// Each QR code module is rendered as a single character (█). The quiet zone adds
    /// additional characters on all sides. If the data is empty or invalid, returns (0,0).
    /// </remarks>
    public override Size Measure(Constraints constraints)
    {
        EnsureMatrix();
        
        if (_matrixSize == 0)
        {
            return constraints.Constrain(new Size(0, 0));
        }

        // Total size = matrix size + quiet zone on both sides
        var totalSize = _matrixSize + (QuietZone * 2);
        return constraints.Constrain(new Size(totalSize, totalSize));
    }

    /// <summary>
    /// Renders the QR code to the terminal using block characters.
    /// </summary>
    /// <param name="context">The render context providing terminal access and inherited styling.</param>
    /// <remarks>
    /// The QR code is rendered using "█" for filled modules and " " for empty modules.
    /// Inherited colors from parent nodes are applied automatically to the block characters.
    /// </remarks>
    public override void Render(Hex1bRenderContext context)
    {
        EnsureMatrix();
        
        if (_matrixSize == 0)
            return;

        var colorCodes = context.Theme.GetGlobalColorCodes();
        var resetCodes = !string.IsNullOrEmpty(colorCodes) ? context.Theme.GetResetToGlobalCodes() : "";

        // Render with quiet zone
        for (int y = 0; y < Bounds.Height && y < _matrixSize + (QuietZone * 2); y++)
        {
            var line = "";
            
            for (int x = 0; x < Bounds.Width && x < _matrixSize + (QuietZone * 2); x++)
            {
                // Check if we're in the quiet zone
                if (x < QuietZone || x >= _matrixSize + QuietZone ||
                    y < QuietZone || y >= _matrixSize + QuietZone)
                {
                    line += EmptySpace; // Quiet zone is empty
                }
                else
                {
                    var matrixX = x - QuietZone;
                    var matrixY = y - QuietZone;
                    
                    // Check if this module is filled
                    var isFilled = matrixY < _matrix!.Count && 
                                   matrixX < _matrix[matrixY].Length && 
                                   _matrix[matrixY][matrixX];
                    
                    line += isFilled ? FilledBlock : EmptySpace;
                }
            }
            
            var renderY = Bounds.Y + y;
            
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.WriteClipped(Bounds.X, renderY, $"{colorCodes}{line}{resetCodes}");
            }
            else
            {
                context.WriteClipped(Bounds.X, renderY, line);
            }
        }
    }
}
