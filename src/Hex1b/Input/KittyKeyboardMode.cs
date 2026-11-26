using System.Globalization;

namespace Hex1b.Input;

/// <summary>
/// Tracks one screen's Kitty keyboard flags. Each screen retains at most 128
/// entries, evicting the oldest on overflow; popping an empty stack yields zero.
/// </summary>
/// <remarks>
/// Like Kitty, flags retain the low seven bits (0-127), including bits the protocol
/// has not yet assigned, through set, push, pop, and query. Higher bits are discarded.
/// Parameters that do not fit in a non-negative <see cref="int"/> invalidate the sequence.
/// The specification leaves the depth to the terminal; Kitty and Ghostty both keep 8
/// entries, while Hex1b keeps 128 so deeply nested programs keep their restore points.
/// </remarks>
internal sealed class KittyKeyboardMode
{
    private const int MaxDepth = 128;
    private const int RetainedFlags = 127;
    private readonly List<int> _stack = [];

    internal int Flags => _stack.Count == 0 ? 0 : _stack[^1];

    internal void Reset() => _stack.Clear();

    internal bool Apply(string sequence, out bool query)
    {
        query = false;
        if (!sequence.StartsWith("\x1b[", StringComparison.Ordinal) || !sequence.EndsWith('u'))
            return false;
        var body = sequence.AsSpan(2, sequence.Length - 3);
        if (body.SequenceEqual("?"))
        {
            query = true;
            return true;
        }
        if (body.IsEmpty || body[0] is not ('=' or '>' or '<'))
            return false;

        var operation = body[0];
        body = body[1..];
        var separator = body.IndexOf(';');
        if (separator >= 0 && operation != '=')
            return false;
        var first = separator < 0 ? body : body[..separator];
        if (!TryParameter(first, operation == '<' ? 1 : 0, out var value))
            return false;
        var mode = 1;
        if (separator >= 0 && !TryParameter(body[(separator + 1)..], 1, out mode))
            return false;
        if (mode is < 1 or > 3)
            return false;

        if (operation == '<')
        {
            var count = Math.Min(value, _stack.Count);
            _stack.RemoveRange(_stack.Count - count, count);
        }
        else if (operation == '>')
        {
            if (_stack.Count == MaxDepth)
                _stack.RemoveAt(0);
            _stack.Add(value & RetainedFlags);
        }
        else
        {
            var flags = value & RetainedFlags;
            var updated = mode switch
            {
                1 => flags,
                2 => Flags | flags,
                _ => Flags & ~flags,
            };
            if (_stack.Count == 0)
                _stack.Add(updated);
            else
                _stack[^1] = updated;
        }
        return true;
    }

    private static bool TryParameter(ReadOnlySpan<char> value, int defaultValue, out int result)
    {
        if (value.IsEmpty)
        {
            result = defaultValue;
            return true;
        }
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }
}
