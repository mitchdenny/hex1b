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
/// QrCodeNode renders QR codes using Unicode half-block characters (▀▄) for compact
/// display — two QR module rows per terminal row. The QR code is generated using the
/// QRCoder library.
/// </para>
/// </remarks>
/// <seealso cref="QrCodeWidget"/>
public sealed class QrCodeNode : Hex1bNode
{
    private string _data = "";
    private List<BitArray>? _matrix;
    private int _matrixSize = 0;
    
    /// <summary>
    /// Gets or sets the data to encode in the QR code.
    /// </summary>
    public string Data 
    { 
        get => _data; 
        set 
        {
            if (_data != value)
            {
                _data = value;
                _matrix = null;
                MarkDirty();
            }
        }
    }
    
    private int _quietZone = 1;
    
    /// <summary>
    /// Gets or sets the number of module widths to use as a border (quiet zone).
    /// </summary>
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
            _matrix = new List<BitArray>();
            _matrixSize = 0;
        }
    }

    /// <summary>
    /// Measures the size required to display the QR code.
    /// Each module is 1 character wide; two module rows are packed into one terminal row
    /// using half-block characters.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        EnsureMatrix();
        
        if (_matrixSize == 0)
            return constraints.Constrain(new Size(0, 0));

        var totalModules = _matrixSize + (QuietZone * 2);
        var width = totalModules;
        var height = (totalModules + 1) / 2;
        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Renders the QR code using half-block characters (▀▄█ and space).
    /// Two module rows are packed per terminal row: the upper half-block represents
    /// the top row and the background color represents the bottom row.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        EnsureMatrix();
        
        if (_matrixSize == 0)
            return;

        var totalModules = _matrixSize + (QuietZone * 2);
        var terminalRows = (totalModules + 1) / 2;

        // QR standard: dark modules on light background
        // We use: fg = color of top row, bg = color of bottom row
        // ▀ = top half filled (fg visible on top, bg on bottom)
        var fgBlack = "\x1b[30m";    // dark module
        var bgBlack = "\x1b[40m";
        var bgWhite = "\x1b[107m";
        var reset = "\x1b[0m";

        for (var row = 0; row < terminalRows && row < Bounds.Height; row++)
        {
            var topModuleRow = row * 2;
            var botModuleRow = topModuleRow + 1;

            var line = "";

            for (var x = 0; x < totalModules; x++)
            {
                var topFilled = IsModuleFilled(x, topModuleRow, totalModules);
                var botFilled = botModuleRow < totalModules && IsModuleFilled(x, botModuleRow, totalModules);

                if (topFilled && botFilled)
                {
                    // Both dark: full block with dark fg (bg doesn't matter)
                    line += $"{fgBlack}{bgBlack}█";
                }
                else if (!topFilled && !botFilled)
                {
                    // Both light: space with white bg
                    line += $"{bgWhite} ";
                }
                else if (topFilled)
                {
                    // Top dark, bottom light: ▀ with black fg, white bg
                    line += $"{fgBlack}{bgWhite}▀";
                }
                else
                {
                    // Top light, bottom dark: ▄ with black fg, white bg
                    line += $"{fgBlack}{bgWhite}▄";
                }
            }

            line += reset;
            context.WriteClipped(Bounds.X, Bounds.Y + row, line);
        }
    }

    /// <summary>
    /// Checks whether a module at the given position is filled (dark).
    /// Quiet zone modules are always empty (light).
    /// </summary>
    private bool IsModuleFilled(int x, int y, int totalModules)
    {
        if (x < QuietZone || x >= _matrixSize + QuietZone ||
            y < QuietZone || y >= _matrixSize + QuietZone)
            return false;

        var matrixX = x - QuietZone;
        var matrixY = y - QuietZone;

        return matrixY < _matrix!.Count &&
               matrixX < _matrix[matrixY].Length &&
               _matrix[matrixY][matrixX];
    }
}
