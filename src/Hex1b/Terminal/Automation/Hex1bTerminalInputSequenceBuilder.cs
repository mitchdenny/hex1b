using Hex1b.Input;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// Fluent builder for creating test sequences to simulate user interaction.
/// </summary>
/// <example>
/// <code>
/// var runTask = app.RunAsync();
/// 
/// await new Hex1bTerminalInputSequenceBuilder()
///     .WaitUntil(s => s.ContainsText("Enter name:"), TimeSpan.FromSeconds(2))
///     .Type("Hello")
///     .Enter()
///     .Ctrl().Key(Hex1bKey.C)
///     .Build()
///     .ApplyAsync(terminal);
///     
/// await runTask;
/// </code>
/// </example>
public sealed class Hex1bTerminalInputSequenceBuilder
{
    internal readonly List<TestStep> _steps = [];
    private Hex1bModifiers _pendingModifiers = Hex1bModifiers.None;
    private int _mouseX;
    private int _mouseY;
    private Hex1bTerminalInputSequenceOptions _options = Hex1bTerminalInputSequenceOptions.Default;

    /// <summary>
    /// Configures options for this sequence.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder WithOptions(Hex1bTerminalInputSequenceOptions options)
    {
        _options = options;
        return this;
    }

    /// <summary>
    /// Configures options for this sequence using a configuration action.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder WithOptions(Action<Hex1bTerminalInputSequenceOptions> configure)
    {
        var options = new Hex1bTerminalInputSequenceOptions();
        configure(options);
        _options = options;
        return this;
    }

    // ========================================
    // Wait conditions
    // ========================================

    /// <summary>
    /// Waits until a condition is met on the terminal.
    /// </summary>
    /// <param name="predicate">The condition to wait for. Receives a snapshot of the terminal state.</param>
    /// <param name="timeout">Maximum time to wait before throwing TimeoutException.</param>
    /// <param name="description">Optional description for error messages.</param>
    public Hex1bTerminalInputSequenceBuilder WaitUntil(
        Func<Hex1bTerminalSnapshot, bool> predicate,
        TimeSpan timeout,
        string? description = null)
    {
        _steps.Add(new WaitUntilStep(predicate, timeout, description));
        return this;
    }

    // ========================================
    // Modifier prefixes
    // ========================================

    /// <summary>
    /// Adds Ctrl modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Ctrl()
    {
        _pendingModifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Adds Shift modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Shift()
    {
        _pendingModifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Adds Alt modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Alt()
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
    public Hex1bTerminalInputSequenceBuilder Key(Hex1bKey key)
    {
        var text = GetDefaultTextForKey(key, _pendingModifiers);
        _steps.Add(new KeyInputStep(key, text, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Sends a key press event with the specified modifiers.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Key(Hex1bKey key, Hex1bModifiers modifiers)
    {
        // Combine with any pending modifiers
        var combinedModifiers = _pendingModifiers | modifiers;
        var text = GetDefaultTextForKey(key, combinedModifiers);
        _steps.Add(new KeyInputStep(key, text, combinedModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Types text quickly (no delay between keystrokes).
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Type(string text) => FastType(text);

    /// <summary>
    /// Types text quickly (no delay between keystrokes).
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder FastType(string text)
    {
        _steps.Add(new TextInputStep(text, TimeSpan.Zero));
        return this;
    }

    /// <summary>
    /// Types text slowly with the default delay between keystrokes.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder SlowType(string text)
    {
        _steps.Add(new TextInputStep(text, _options.SlowTypeDelay));
        return this;
    }

    /// <summary>
    /// Types text slowly with a custom delay between keystrokes.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder SlowType(string text, TimeSpan delay)
    {
        _steps.Add(new TextInputStep(text, delay));
        return this;
    }

    // ========================================
    // Common key shortcuts
    // ========================================

    /// <summary>Sends Enter key.</summary>
    public Hex1bTerminalInputSequenceBuilder Enter() => Key(Hex1bKey.Enter);

    /// <summary>Sends Tab key.</summary>
    public Hex1bTerminalInputSequenceBuilder Tab() => Key(Hex1bKey.Tab);

    /// <summary>Sends Escape key.</summary>
    public Hex1bTerminalInputSequenceBuilder Escape() => Key(Hex1bKey.Escape);

    /// <summary>Sends Backspace key.</summary>
    public Hex1bTerminalInputSequenceBuilder Backspace() => Key(Hex1bKey.Backspace);

    /// <summary>Sends Delete key.</summary>
    public Hex1bTerminalInputSequenceBuilder Delete() => Key(Hex1bKey.Delete);

    /// <summary>Sends Space key.</summary>
    public Hex1bTerminalInputSequenceBuilder Space() => Key(Hex1bKey.Spacebar);

    /// <summary>Sends Up arrow key.</summary>
    public Hex1bTerminalInputSequenceBuilder Up() => Key(Hex1bKey.UpArrow);

    /// <summary>Sends Down arrow key.</summary>
    public Hex1bTerminalInputSequenceBuilder Down() => Key(Hex1bKey.DownArrow);

    /// <summary>Sends Left arrow key.</summary>
    public Hex1bTerminalInputSequenceBuilder Left() => Key(Hex1bKey.LeftArrow);

    /// <summary>Sends Right arrow key.</summary>
    public Hex1bTerminalInputSequenceBuilder Right() => Key(Hex1bKey.RightArrow);

    /// <summary>Sends Home key.</summary>
    public Hex1bTerminalInputSequenceBuilder Home() => Key(Hex1bKey.Home);

    /// <summary>Sends End key.</summary>
    public Hex1bTerminalInputSequenceBuilder End() => Key(Hex1bKey.End);

    /// <summary>Sends Page Up key.</summary>
    public Hex1bTerminalInputSequenceBuilder PageUp() => Key(Hex1bKey.PageUp);

    /// <summary>Sends Page Down key.</summary>
    public Hex1bTerminalInputSequenceBuilder PageDown() => Key(Hex1bKey.PageDown);

    // ========================================
    // Mouse input
    // ========================================

    /// <summary>
    /// Moves the mouse to an absolute position.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder MouseMoveTo(int x, int y)
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
    public Hex1bTerminalInputSequenceBuilder MouseMove(int deltaX, int deltaY)
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
    public Hex1bTerminalInputSequenceBuilder MouseDown(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Releases a mouse button at the current position.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder MouseUp(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a click (down + up) at the current position.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Click(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers, ClickCount: 1));
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers, ClickCount: 1));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a click at the specified position.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder ClickAt(int x, int y, MouseButton button = MouseButton.Left)
    {
        _mouseX = x;
        _mouseY = y;
        return Click(button);
    }

    /// <summary>
    /// Performs a double-click at the current position.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder DoubleClick(MouseButton button = MouseButton.Left)
    {
        _steps.Add(new MouseInputStep(button, MouseAction.Down, _mouseX, _mouseY, _pendingModifiers, ClickCount: 2));
        _steps.Add(new MouseInputStep(button, MouseAction.Up, _mouseX, _mouseY, _pendingModifiers, ClickCount: 2));
        _pendingModifiers = Hex1bModifiers.None;
        return this;
    }

    /// <summary>
    /// Performs a drag from one position to another.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Drag(int fromX, int fromY, int toX, int toY, MouseButton button = MouseButton.Left)
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
    public Hex1bTerminalInputSequenceBuilder ScrollUp(int ticks = 1)
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
    public Hex1bTerminalInputSequenceBuilder ScrollDown(int ticks = 1)
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
    public Hex1bTerminalInputSequenceBuilder Wait(TimeSpan duration)
    {
        _steps.Add(new WaitStep(duration));
        return this;
    }

    /// <summary>
    /// Pauses for the specified number of milliseconds.
    /// </summary>
    public Hex1bTerminalInputSequenceBuilder Wait(int milliseconds)
        => Wait(TimeSpan.FromMilliseconds(milliseconds));

    // ========================================
    // Build
    // ========================================

    /// <summary>
    /// Builds the test sequence.
    /// </summary>
    public Hex1bTerminalInputSequence Build()
    {
        return new Hex1bTerminalInputSequence(_steps.ToList(), _options);
    }

    // ========================================
    // Helpers
    // ========================================

    private static string GetDefaultTextForKey(Hex1bKey key, Hex1bModifiers modifiers)
    {
        var isShift = (modifiers & Hex1bModifiers.Shift) != 0;
        var isControl = (modifiers & Hex1bModifiers.Control) != 0;
        var isAlt = (modifiers & Hex1bModifiers.Alt) != 0;
        
        // Control/Alt combos with letters don't produce printable text
        // (they're keyboard shortcuts, not character input)
        if ((isControl || isAlt) && key >= Hex1bKey.A && key <= Hex1bKey.Z)
        {
            return "";
        }
        
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
