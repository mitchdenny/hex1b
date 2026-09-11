using System.Globalization;

namespace Hex1b.Automation;

internal static class TapeRuntimeLiteral
{
    internal static bool TryDuration(string text, out TimeSpan value)
    {
        var multiplier = text.EndsWith("ms", StringComparison.Ordinal) ? .001
            : text.EndsWith('m') ? 60 : 1;
        var number = text.EndsWith("ms", StringComparison.Ordinal) ? text[..^2]
            : text.EndsWith('m') || text.EndsWith('s') ? text[..^1] : text;
        if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed) || parsed < 0 || parsed * multiplier > (uint.MaxValue - 1) / 1000d)
        {
            value = default;
            return false;
        }
        value = TimeSpan.FromSeconds(parsed * multiplier);
        return true;
    }

    internal static bool TryRepeat(string? text, out ulong count)
    {
        count = 1;
        if (text is null)
            return true;
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            return false;
        count = (ulong)parsed;
        return true;
    }
}
