using System.Text;
using System.Text.RegularExpressions;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation adapter decorator that applies a "fog of war" effect,
/// dimming colors based on distance from the mouse cursor.
/// </summary>
/// <remarks>
/// <para>
/// This filter intercepts ANSI output sequences and modifies foreground/background
/// colors so that cells farther from the mouse cursor appear darker (closer to black).
/// </para>
/// <para>
/// The effect creates a spotlight-like appearance where only the area near the
/// mouse cursor is fully visible, while distant areas fade to black.
/// </para>
/// </remarks>
public sealed class FogOfWarPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _inner;
    private int _mouseX = -1;
    private int _mouseY = -1;
    private int _currentX;
    private int _currentY;
    private readonly double _maxDistance;

    /// <summary>
    /// Creates a new fog of war presentation adapter wrapping the specified adapter.
    /// </summary>
    /// <param name="inner">The underlying presentation adapter.</param>
    /// <param name="maxDistance">The maximum distance (in cells) at which colors are completely black. Default is 20.</param>
    public FogOfWarPresentationAdapter(IHex1bTerminalPresentationAdapter inner, double maxDistance = 20.0)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxDistance = maxDistance;
    }

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized
    {
        add => _inner.Resized += value;
        remove => _inner.Resized -= value;
    }

    /// <inheritdoc />
    public event Action? Disconnected
    {
        add => _inner.Disconnected += value;
        remove => _inner.Disconnected -= value;
    }

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (data.IsEmpty)
        {
            await _inner.WriteOutputAsync(data, ct);
            return;
        }

        var text = Encoding.UTF8.GetString(data.Span);
        var modified = ApplyFogOfWar(text);
        var modifiedBytes = Encoding.UTF8.GetBytes(modified);
        await _inner.WriteOutputAsync(modifiedBytes, ct);
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        var input = await _inner.ReadInputAsync(ct);
        
        // Track mouse position by parsing SGR mouse sequences
        if (!input.IsEmpty)
        {
            var text = Encoding.UTF8.GetString(input.Span);
            ParseMousePosition(text);
        }
        
        return input;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default) => _inner.FlushAsync(ct);

    /// <inheritdoc />
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default) => _inner.EnterTuiModeAsync(ct);

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default) => _inner.ExitTuiModeAsync(ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private void ParseMousePosition(string text)
    {
        // SGR mouse format: ESC [ < Cb ; Cx ; Cy M/m
        // Example: \x1b[<0;10;5M (button 0, column 10, row 5, pressed)
        var match = Regex.Match(text, @"\x1b\[<(\d+);(\d+);(\d+)[Mm]");
        if (match.Success)
        {
            _mouseX = int.Parse(match.Groups[2].Value) - 1; // Convert to 0-based
            _mouseY = int.Parse(match.Groups[3].Value) - 1; // Convert to 0-based
        }
    }

    private string ApplyFogOfWar(string text)
    {
        // If no mouse position is known yet, pass through unchanged
        if (_mouseX < 0 || _mouseY < 0)
        {
            return text;
        }

        var result = new StringBuilder(text.Length * 2);
        var i = 0;

        while (i < text.Length)
        {
            // Check for cursor positioning: ESC [ row ; col H
            if (i < text.Length - 1 && text[i] == '\x1b' && text[i + 1] == '[')
            {
                var seqStart = i;
                i += 2;
                var seqEnd = i;
                
                // Find the end of the sequence
                while (seqEnd < text.Length && !char.IsLetter(text[seqEnd]))
                {
                    seqEnd++;
                }
                
                if (seqEnd < text.Length)
                {
                    var command = text[seqEnd];
                    var parameters = text[i..seqEnd];
                    
                    // Track cursor position
                    if (command == 'H' || command == 'f')
                    {
                        var parts = parameters.Split(';');
                        if (parts.Length >= 2)
                        {
                            if (int.TryParse(parts[0], out var row) && int.TryParse(parts[1], out var col))
                            {
                                _currentY = row - 1; // Convert to 0-based
                                _currentX = col - 1;
                            }
                        }
                        else if (parts.Length == 1 && string.IsNullOrEmpty(parts[0]))
                        {
                            // ESC[H moves to 0,0
                            _currentY = 0;
                            _currentX = 0;
                        }
                    }
                    
                    // Check for SGR (color) sequences
                    if (command == 'm')
                    {
                        var modifiedSeq = ModifyColorSequence(parameters, _currentX, _currentY);
                        result.Append("\x1b[");
                        result.Append(modifiedSeq);
                        result.Append('m');
                        i = seqEnd + 1;
                        continue;
                    }
                    
                    // Pass through other sequences unchanged
                    result.Append(text[seqStart..(seqEnd + 1)]);
                    i = seqEnd + 1;
                    continue;
                }
            }
            
            // Handle newline and carriage return
            if (text[i] == '\n')
            {
                _currentY++;
                _currentX = 0;
            }
            else if (text[i] == '\r')
            {
                _currentX = 0;
            }
            else if (text[i] >= 32) // Printable character
            {
                _currentX++;
            }
            
            result.Append(text[i]);
            i++;
        }

        return result.ToString();
    }

    private string ModifyColorSequence(string parameters, int x, int y)
    {
        // Calculate distance from mouse cursor
        var distance = Math.Sqrt((x - _mouseX) * (x - _mouseX) + (y - _mouseY) * (y - _mouseY));
        
        // Calculate fog factor (0 = completely fogged/black, 1 = no fog)
        var fogFactor = Math.Max(0.0, 1.0 - (distance / _maxDistance));
        
        // Parse and modify the SGR parameters
        var parts = parameters.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code))
            {
                result.Add(parts[i]);
                continue;
            }
            
            // Handle 24-bit RGB foreground: 38;2;r;g;b
            if (code == 38 && i + 4 < parts.Length && parts[i + 1] == "2")
            {
                if (int.TryParse(parts[i + 2], out var r) &&
                    int.TryParse(parts[i + 3], out var g) &&
                    int.TryParse(parts[i + 4], out var b))
                {
                    var modifiedR = (byte)(r * fogFactor);
                    var modifiedG = (byte)(g * fogFactor);
                    var modifiedB = (byte)(b * fogFactor);
                    
                    result.Add("38");
                    result.Add("2");
                    result.Add(modifiedR.ToString());
                    result.Add(modifiedG.ToString());
                    result.Add(modifiedB.ToString());
                    i += 4;
                    continue;
                }
            }
            
            // Handle 24-bit RGB background: 48;2;r;g;b
            if (code == 48 && i + 4 < parts.Length && parts[i + 1] == "2")
            {
                if (int.TryParse(parts[i + 2], out var r) &&
                    int.TryParse(parts[i + 3], out var g) &&
                    int.TryParse(parts[i + 4], out var b))
                {
                    var modifiedR = (byte)(r * fogFactor);
                    var modifiedG = (byte)(g * fogFactor);
                    var modifiedB = (byte)(b * fogFactor);
                    
                    result.Add("48");
                    result.Add("2");
                    result.Add(modifiedR.ToString());
                    result.Add(modifiedG.ToString());
                    result.Add(modifiedB.ToString());
                    i += 4;
                    continue;
                }
            }
            
            // Handle 256-color foreground: 38;5;n
            if (code == 38 && i + 2 < parts.Length && parts[i + 1] == "5")
            {
                if (int.TryParse(parts[i + 2], out var colorIndex))
                {
                    var (r, g, b) = Get256ColorRgb(colorIndex);
                    var modifiedR = (byte)(r * fogFactor);
                    var modifiedG = (byte)(g * fogFactor);
                    var modifiedB = (byte)(b * fogFactor);
                    
                    // Convert to 24-bit RGB for consistent fog application
                    result.Add("38");
                    result.Add("2");
                    result.Add(modifiedR.ToString());
                    result.Add(modifiedG.ToString());
                    result.Add(modifiedB.ToString());
                    i += 2;
                    continue;
                }
            }
            
            // Handle 256-color background: 48;5;n
            if (code == 48 && i + 2 < parts.Length && parts[i + 1] == "5")
            {
                if (int.TryParse(parts[i + 2], out var colorIndex))
                {
                    var (r, g, b) = Get256ColorRgb(colorIndex);
                    var modifiedR = (byte)(r * fogFactor);
                    var modifiedG = (byte)(g * fogFactor);
                    var modifiedB = (byte)(b * fogFactor);
                    
                    // Convert to 24-bit RGB for consistent fog application
                    result.Add("48");
                    result.Add("2");
                    result.Add(modifiedR.ToString());
                    result.Add(modifiedG.ToString());
                    result.Add(modifiedB.ToString());
                    i += 2;
                    continue;
                }
            }
            
            // Handle basic ANSI colors (30-37 foreground, 40-47 background, 90-97, 100-107)
            if ((code >= 30 && code <= 37) || (code >= 90 && code <= 97))
            {
                // Foreground color
                var (r, g, b) = GetBasicColorRgb(code);
                var modifiedR = (byte)(r * fogFactor);
                var modifiedG = (byte)(g * fogFactor);
                var modifiedB = (byte)(b * fogFactor);
                
                result.Add("38");
                result.Add("2");
                result.Add(modifiedR.ToString());
                result.Add(modifiedG.ToString());
                result.Add(modifiedB.ToString());
                continue;
            }
            
            if ((code >= 40 && code <= 47) || (code >= 100 && code <= 107))
            {
                // Background color
                var (r, g, b) = GetBasicColorRgb(code);
                var modifiedR = (byte)(r * fogFactor);
                var modifiedG = (byte)(g * fogFactor);
                var modifiedB = (byte)(b * fogFactor);
                
                result.Add("48");
                result.Add("2");
                result.Add(modifiedR.ToString());
                result.Add(modifiedG.ToString());
                result.Add(modifiedB.ToString());
                continue;
            }
            
            // Pass through other codes unchanged (reset, bold, italic, etc.)
            result.Add(parts[i]);
        }
        
        return string.Join(";", result);
    }

    private static (byte r, byte g, byte b) GetBasicColorRgb(int code)
    {
        // Map ANSI basic colors to RGB
        // 30-37: normal foreground, 40-47: normal background
        // 90-97: bright foreground, 100-107: bright background
        var baseColor = code >= 90 ? code - 90 : code >= 40 ? code - 40 : code - 30;
        var bright = code >= 90 || (code >= 100 && code <= 107);
        
        return (baseColor % 10) switch
        {
            0 => bright ? ((byte)128, (byte)128, (byte)128) : ((byte)0, (byte)0, (byte)0),       // Black / Bright Black (Gray)
            1 => bright ? ((byte)255, (byte)0, (byte)0) : ((byte)170, (byte)0, (byte)0),         // Red / Bright Red
            2 => bright ? ((byte)0, (byte)255, (byte)0) : ((byte)0, (byte)170, (byte)0),         // Green / Bright Green
            3 => bright ? ((byte)255, (byte)255, (byte)0) : ((byte)170, (byte)85, (byte)0),      // Yellow / Bright Yellow
            4 => bright ? ((byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)170),         // Blue / Bright Blue
            5 => bright ? ((byte)255, (byte)0, (byte)255) : ((byte)170, (byte)0, (byte)170),     // Magenta / Bright Magenta
            6 => bright ? ((byte)0, (byte)255, (byte)255) : ((byte)0, (byte)170, (byte)170),     // Cyan / Bright Cyan
            7 => bright ? ((byte)255, (byte)255, (byte)255) : ((byte)170, (byte)170, (byte)170), // White / Bright White
            _ => ((byte)0, (byte)0, (byte)0)
        };
    }

    private static (byte r, byte g, byte b) Get256ColorRgb(int index)
    {
        // 256-color palette mapping
        // 0-15: Basic ANSI colors
        if (index < 16)
        {
            return index switch
            {
                0 => (0, 0, 0),
                1 => (170, 0, 0),
                2 => (0, 170, 0),
                3 => (170, 85, 0),
                4 => (0, 0, 170),
                5 => (170, 0, 170),
                6 => (0, 170, 170),
                7 => (170, 170, 170),
                8 => (85, 85, 85),
                9 => (255, 85, 85),
                10 => (85, 255, 85),
                11 => (255, 255, 85),
                12 => (85, 85, 255),
                13 => (255, 85, 255),
                14 => (85, 255, 255),
                15 => (255, 255, 255),
                _ => (0, 0, 0)
            };
        }
        
        // 16-231: 6x6x6 color cube
        if (index < 232)
        {
            var colorIndex = index - 16;
            var r = (colorIndex / 36) % 6;
            var g = (colorIndex / 6) % 6;
            var b = colorIndex % 6;
            
            return (
                (byte)(r == 0 ? 0 : 55 + r * 40),
                (byte)(g == 0 ? 0 : 55 + g * 40),
                (byte)(b == 0 ? 0 : 55 + b * 40)
            );
        }
        
        // 232-255: Grayscale ramp
        var gray = (byte)(8 + (index - 232) * 10);
        return (gray, gray, gray);
    }
}
