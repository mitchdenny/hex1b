namespace Hex1b.Input;

/// <summary>
/// Parses mouse escape sequences from the terminal.
/// Supports SGR extended mouse mode (\e[&lt;...M/m) which is the most portable.
/// </summary>
public static class MouseParser
{
    /// <summary>
    /// ANSI escape sequence to enable SGR extended mouse mode with motion tracking.
    /// </summary>
    public const string EnableMouseTracking = 
        "\x1b[?1000h" +  // Enable mouse button events
        "\x1b[?1002h" +  // Enable button-motion events (drag)
        "\x1b[?1003h" +  // Enable all motion events
        "\x1b[?1006h";   // Enable SGR extended mode (supports coordinates > 223)
    
    /// <summary>
    /// ANSI escape sequence to disable mouse tracking.
    /// </summary>
    public const string DisableMouseTracking = 
        "\x1b[?1006l" +  // Disable SGR extended mode
        "\x1b[?1003l" +  // Disable all motion events
        "\x1b[?1002l" +  // Disable button-motion events
        "\x1b[?1000l";   // Disable mouse button events
    
    /// <summary>
    /// Tries to parse a mouse escape sequence.
    /// SGR format: \e[&lt;Cb;Cx;CyM (button down) or \e[&lt;Cb;Cx;Cym (button up)
    /// Where Cb is button code, Cx is 1-based column, Cy is 1-based row.
    /// </summary>
    /// <param name="sequence">The escape sequence to parse (without the leading \e[&lt;).</param>
    /// <param name="mouseEvent">The parsed mouse event if successful.</param>
    /// <returns>True if the sequence was a valid mouse event.</returns>
    public static bool TryParseSgr(string sequence, out Hex1bMouseEvent? mouseEvent)
    {
        mouseEvent = null;
        
        // Check for the terminating character (M for down/move, m for up)
        if (sequence.Length < 5) return false;
        
        var terminator = sequence[^1];
        if (terminator != 'M' && terminator != 'm') return false;
        
        // Parse the parameters (button;x;y)
        var paramPart = sequence[..^1];
        var parts = paramPart.Split(';');
        if (parts.Length != 3) return false;
        
        if (!int.TryParse(parts[0], out var buttonCode) ||
            !int.TryParse(parts[1], out var x) ||
            !int.TryParse(parts[2], out var y))
        {
            return false;
        }
        
        // Convert to 0-based coordinates
        x--;
        y--;
        
        // Parse modifiers from button code
        var modifiers = Hex1bModifiers.None;
        if ((buttonCode & 4) != 0) modifiers |= Hex1bModifiers.Shift;
        if ((buttonCode & 8) != 0) modifiers |= Hex1bModifiers.Alt;
        if ((buttonCode & 16) != 0) modifiers |= Hex1bModifiers.Control;
        
        // Determine button and action
        var isMotion = (buttonCode & 32) != 0;
        var baseButton = buttonCode & 3;  // Lower 2 bits for button
        var isScrollUp = (buttonCode & 64) != 0 && baseButton == 0;
        var isScrollDown = (buttonCode & 64) != 0 && baseButton == 1;
        
        MouseButton button;
        MouseAction action;
        
        if (isScrollUp)
        {
            button = MouseButton.ScrollUp;
            action = MouseAction.Down;
        }
        else if (isScrollDown)
        {
            button = MouseButton.ScrollDown;
            action = MouseAction.Down;
        }
        else if (isMotion)
        {
            // Motion event
            if (baseButton == 3)
            {
                // No button pressed - just movement
                button = MouseButton.None;
                action = MouseAction.Move;
            }
            else
            {
                // Button held during motion - drag
                button = baseButton switch
                {
                    0 => MouseButton.Left,
                    1 => MouseButton.Middle,
                    2 => MouseButton.Right,
                    _ => MouseButton.None
                };
                action = MouseAction.Drag;
            }
        }
        else
        {
            // Button press/release
            button = baseButton switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                3 => MouseButton.None,  // Release (in non-SGR mode)
                _ => MouseButton.None
            };
            action = terminator == 'M' ? MouseAction.Down : MouseAction.Up;
        }
        
        mouseEvent = new Hex1bMouseEvent(button, action, x, y, modifiers);
        return true;
    }
}
