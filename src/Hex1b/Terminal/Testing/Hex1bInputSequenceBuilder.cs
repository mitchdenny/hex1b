using Hex1b.Input;

namespace Hex1b.Terminal.Testing;

/// <summary>
/// Fluent builder for creating input sequences to simulate user interaction.
/// </summary>
/// <example>
/// <code>
/// var sequence = new Hex1bInputSequenceBuilder()
///     .Type("Hello")
///     .Tab()
///     .Type("World")
///     .Enter()
///     .Build();
/// 
/// await sequence.ApplyAsync(terminal);
/// </code>
/// </example>
public sealed class Hex1bInputSequenceBuilder
{
    private readonly List<InputStep> _steps = [];
    private Hex1bModifiers _pendingModifiers = Hex1bModifiers.None;
    private int _mouseX;
    private int _mouseY;

    /// <summary>
    /// Default delay between keystrokes for SlowType.
    /// </summary>
    public static readonly TimeSpan DefaultSlowTypeDelay = TimeSpan.FromMilliseconds(50);

    // ========================================
    // Modifier prefixes
    // ========================================

    /// <summary>
    /// Adds Ctrl modifier to the next key or mouse action.
    /// </summary>
    public Hex1bInputSequenceBuilder Ctrl()
    {
        _pendingModifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Adds Shift modifier to the next key or mouse action.
    /// </summary>
    public Hex1bInputSequenceBuilder Shift()
    {
        _pendingModifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Adds Alt modifier to the next key or mouse action.
    /// </summary>
    public Hex1bInputSequenceBuilder Alt()
    {
        _pendingModifiers |= Hex1bModifiers.Alt;
        return this;
    }

    // ========================================
    // Key input
    // ========================================

    /// <summary>
    /// Sends a key press event.
    /// </summary>
    public Hex1bInputSequenceBuilder Key(Hex1bKey key)
    {
        var text = GetDefaultTextForKey(key, _pendingModifiers);
        _steps.Add(new KeyInputStep(key, text, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Types text quickly (no delay between keystrokes).
    /// </summary>
    public Hex1bInputSequenceBuilder Type(string text) => FastType(text);

    /// <summary>
    /// Types text quickly (no delay between keystrokes).
    /// </summary>
    public Hex1bInputSequenceBuilder FastType(string text)
    {
        _steps.Add(new TextInputStep(text, TimeSpan.Zero));
        return this;
    }

    /// <summary>
    /// Types text slowly with the default delay between keystrokes.
    /// </summary>
    public Hex1bInputSequenceBuilder SlowType(string text)
        => SlowType(text, DefaultSlowTypeDelay);

    /// <summary>
    /// Types text slowly with a custom delay between keystrokes.
    /// </summary>
    public Hex1bInputSequenceBuilder SlowType(string text, TimeSpan delay)
    {
        _steps.Add(new TextInputStep(text, delay));
        return this;
    }

    // ========================================
    // Common key shortcuts
    // ========================================

    /// <summary>Sends Enter key.</summary>
    public Hex1bInputSequenceBuilder Enter() => Key(Hex1bKey.Enter);

    /// <summary>Sends Tab key.</summary>
    public Hex1bInputSequenceBuilder Tab() => Key(Hex1bKey.Tab);

    /// <summary>Sends Escape key.</summary>
    public Hex1bInputSequenceBuilder Escape() => Key(Hex1bKey.Escape);

    /// <summary>Sends Backspace key.</summary>
    public Hex1bInputSequenceBuilder Backspace() => Key(Hex1bKey.Backspace);

    /// <summary>Sends Delete key.</summary>
    public Hex1bInputSequenceBuilder Delete() => Key(Hex1bKey.Delete);

    /// <summary>Sends Space key.</summary>
    public Hex1bInputSequenceBuilder Space() => Key(Hex1bKey.Spacebar);

    /// <summary>Sends Up arrow key.</summary>
    public Hex1bInputSequenceBuilder Up() => Key(Hex1bKey.UpArrow);

    /// <summary>Sends Down arrow key.</summary>
    public Hex1bInputSequenceBuilder Down() => Key(Hex1bKey.DownArrow);

    /// <summary>Sends Left arrow key.</summary>
    public Hex1bInputSequenceBuilder Left() => Key(Hex1bKey.LeftArrow);

    /// <summary>Sends Right arrow key.</summary>
    public Hex1bInputSequenceBuilder Right() => Key(Hex1bKey.RightArrow);

    /// <summary>Sends Home key.</summary>
    public Hex1bInputSequenceBuilder Home() => Key(Hex1bKey.Home);

    /// <summary>Sends End key.</summary>
    public Hex1bInputSequenceBuilder End() => Key(Hex1bKey.End);

    /// <summary>Sends Page Up key.</summary>
    public Hex1bInputSequenceBuilder PageUp() => Key(Hex1bKey.PageUp);

    /// <summary>Sends Page Down key.</summary>
    public Hex1bInputSequenceBuilder PageDown() => Key(Hex1bKey.PageDown);

    // ========================================
    // Mouse input
    // ========================================

    /// <summary>
    /// Moves the mouse to an absolute position.
    /// </summary>
    public Hex1bInputSequenceBuilder MouseMoveTo(int x, int y)
    {
        _mouseX = x;
        _mouseY = y;
        _steps.Add(new MouseInputStep(MouseButton.None, MouseAction.Move, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Moves the mouse by a relative delta.
    /// </summary>
    public Hex1bInputSequenceBuilder MouseMove(int deltaX, int deltaY)
    {
        _mouseX += deltaX;
        _mouseY += deltaY;
        _steps.Add(new MouseInputStep(MouseButton.None, MouseAction.Move, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Presses a mouse button down at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder MouseDown(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Releases a mouse button at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder MouseUp(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a click (down + up) at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder Click(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers, ClickCount: 1));
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers, ClickCount: 1));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a click at the specified position.
    /// </summary>
    public Hex1bInputSequenceBuilder ClickAt(int x, int y, MouseButton button = MouseButton.Left)
    {
        _mouseX = x;
        _mouseY = y;
        return Click(button);
    }

    /// <summary>
    /// Performs a double-click at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder DoubleClick(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers, ClickCount: 2));
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers, ClickCount: 2));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a drag from one position to another.
    /// </summary>
    public Hex1bInputSequenceBuilder Drag(int fromX, int fromY, int toX, int toY, MouseButton button = MouseButton.Left)
    {
        _mouseX = fromX;
        _mouseY = fromY;
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers));
        
        _mouseX = toX;
        _mouseY = toY;
        _steps.Add(new MouseInputStep(button, MouseAction.Drag, _mouseX, _mouseY, _pendingModifiers));
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers));
        
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Scrolls up at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder ScrollUp(int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            _steps.Add(new MouseInputStep(MouseButton.ScrollUp, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers));
        }
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Scrolls down at the current position.
    /// </summary>
    public Hex1bInputSequenceBuilder ScrollDown(int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            _steps.Add(new MouseInputStep(MouseButton.ScrollDown, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers));
        }
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    // ========================================
    // Timing
    // ========================================

    /// <summary>
    /// Pauses for the specified duration.
    /// </summary>
    public Hex1bInputSequenceBuilder Wait(TimeSpan duration)
    {
        _steps.Add(new WaitStep(duration));
        return this;
    }

    /// <summary>
    /// Pauses for the specified number of milliseconds.
    /// </summary>
    public Hex1bInputSequenceBuilder Wait(int milliseconds)
        => Wait(TimeSpan.FromMilliseconds(milliseconds));

    // ========================================
    // Build
    // ========================================

    /// <summary>
    /// Builds the input sequence.
    /// </summary>
    public Hex1bInputSequence Build()
    {
        return new Hex1bInputSequence(_steps.ToList());
    }

    // ========================================
    // Helpers
    // ========================================

    private static string GetDefaultTextForKey(Hex1bKey key, Hex1bModifiers modifiers)
    {
        var isShift = (modifiers & Hex1bModifiers.Shift) != 0;
        
        return key switch
        {
            >= Hex1bKey.A and <= Hex1bKey.Z => isShift 
                ? ((char)('A' + (key - Hex1bKey.A))).ToString()
                : ((char)('a' + (key - Hex1bKey.A))).ToString(),
            >= Hex1bKey.D0 and <= Hex1bKey.D9 when !isShift => ((char)('0' + (key - Hex1bKey.D0))).ToString(),
            Hex1bKey.D1 when isShift => "!",
            Hex1bKey.D2 when isShift => "@",
            Hex1bKey.D3 when isShift => "#",
            Hex1bKey.D4 when isShift => "$",
            Hex1bKey.D5 when isShift => "%",
            Hex1bKey.D6 when isShift => "^",
            Hex1bKey.D7 when isShift => "&",
            Hex1bKey.D8 when isShift => "*",
            Hex1bKey.D9 when isShift => "(",
            Hex1bKey.D0 when isShift => ")",
            Hex1bKey.Spacebar => " ",
            Hex1bKey.Tab => "\t",
            Hex1bKey.Enter => "\r",
            Hex1bKey.OemPeriod => isShift ? ">" : ".",
            Hex1bKey.OemComma => isShift ? "<" : ",",
            Hex1bKey.OemMinus => isShift ? "_" : "-",
            Hex1bKey.OemPlus => isShift ? "+" : "=",
            Hex1bKey.Oem1 => isShift ? ":" : ";",
            Hex1bKey.Oem7 => isShift ? "\"" : "'",
            Hex1bKey.Oem4 => isShift ? "{" : "[",
            Hex1bKey.Oem6 => isShift ? "}" : "]",
            Hex1bKey.Oem5 => isShift ? "|" : "\\",
            Hex1bKey.OemQuestion => isShift ? "?" : "/",
            Hex1bKey.OemTilde => isShift ? "~" : "`",
            _ => "",
        };
    }
}
