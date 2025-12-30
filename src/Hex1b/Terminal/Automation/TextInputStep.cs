using Hex1b.Input;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// A step that types a string of text, optionally with delays between keystrokes.
/// </summary>
public sealed record TextInputStep(string Text, TimeSpan DelayBetweenKeys) : TestStep
{
    internal override async Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        foreach (var c in Text)
        {
            ct.ThrowIfCancellationRequested();
            
            var evt = CharToKeyEvent(c);
            terminal.SendEvent(evt);
            
            if (DelayBetweenKeys > TimeSpan.Zero)
            {
                if (options.TimeProvider is { } timeProvider)
                {
                    await DelayAsync(timeProvider, DelayBetweenKeys, ct);
                    terminal.FlushOutput();
                }
                else
                {
                    await Task.Delay(DelayBetweenKeys, ct);
                }
            }
        }
    }

    private static Hex1bKeyEvent CharToKeyEvent(char c)
    {
        var (key, modifiers) = c switch
        {
            >= 'a' and <= 'z' => ((Hex1bKey)((int)Hex1bKey.A + (c - 'a')), Hex1bModifiers.None),
            >= 'A' and <= 'Z' => ((Hex1bKey)((int)Hex1bKey.A + (c - 'A')), Hex1bModifiers.Shift),
            >= '0' and <= '9' => ((Hex1bKey)((int)Hex1bKey.D0 + (c - '0')), Hex1bModifiers.None),
            ' ' => (Hex1bKey.Spacebar, Hex1bModifiers.None),
            '\t' => (Hex1bKey.Tab, Hex1bModifiers.None),
            '\r' or '\n' => (Hex1bKey.Enter, Hex1bModifiers.None),
            '\b' => (Hex1bKey.Backspace, Hex1bModifiers.None),
            
            // Common punctuation (US keyboard layout)
            '.' => (Hex1bKey.OemPeriod, Hex1bModifiers.None),
            ',' => (Hex1bKey.OemComma, Hex1bModifiers.None),
            '-' => (Hex1bKey.OemMinus, Hex1bModifiers.None),
            '=' => (Hex1bKey.OemPlus, Hex1bModifiers.None),
            ';' => (Hex1bKey.Oem1, Hex1bModifiers.None),
            '\'' => (Hex1bKey.Oem7, Hex1bModifiers.None),
            '[' => (Hex1bKey.Oem4, Hex1bModifiers.None),
            ']' => (Hex1bKey.Oem6, Hex1bModifiers.None),
            '\\' => (Hex1bKey.Oem5, Hex1bModifiers.None),
            '/' => (Hex1bKey.OemQuestion, Hex1bModifiers.None),
            '`' => (Hex1bKey.OemTilde, Hex1bModifiers.None),
            
            // Shifted punctuation (US keyboard layout)
            '!' => (Hex1bKey.D1, Hex1bModifiers.Shift),
            '@' => (Hex1bKey.D2, Hex1bModifiers.Shift),
            '#' => (Hex1bKey.D3, Hex1bModifiers.Shift),
            '$' => (Hex1bKey.D4, Hex1bModifiers.Shift),
            '%' => (Hex1bKey.D5, Hex1bModifiers.Shift),
            '^' => (Hex1bKey.D6, Hex1bModifiers.Shift),
            '&' => (Hex1bKey.D7, Hex1bModifiers.Shift),
            '*' => (Hex1bKey.D8, Hex1bModifiers.Shift),
            '(' => (Hex1bKey.D9, Hex1bModifiers.Shift),
            ')' => (Hex1bKey.D0, Hex1bModifiers.Shift),
            '_' => (Hex1bKey.OemMinus, Hex1bModifiers.Shift),
            '+' => (Hex1bKey.OemPlus, Hex1bModifiers.Shift),
            ':' => (Hex1bKey.Oem1, Hex1bModifiers.Shift),
            '"' => (Hex1bKey.Oem7, Hex1bModifiers.Shift),
            '{' => (Hex1bKey.Oem4, Hex1bModifiers.Shift),
            '}' => (Hex1bKey.Oem6, Hex1bModifiers.Shift),
            '|' => (Hex1bKey.Oem5, Hex1bModifiers.Shift),
            '<' => (Hex1bKey.OemComma, Hex1bModifiers.Shift),
            '>' => (Hex1bKey.OemPeriod, Hex1bModifiers.Shift),
            '?' => (Hex1bKey.OemQuestion, Hex1bModifiers.Shift),
            '~' => (Hex1bKey.OemTilde, Hex1bModifiers.Shift),
            
            // Default: use None key with the character as text
            _ => (Hex1bKey.None, Hex1bModifiers.None),
        };
        
        return new Hex1bKeyEvent(key, c.ToString(), modifiers);
    }
}
