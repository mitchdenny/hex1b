using System.Text;

namespace Hex1b;

internal static class TerminalTitle
{
    internal const int MaxLength = 4096;

    internal static string Normalize(string value)
    {
        var result = new StringBuilder(Math.Min(value.Length, MaxLength));
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsControl(character))
                continue;
            if (result.Length == MaxLength)
                break;

            if (char.IsHighSurrogate(character) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                if (result.Length + 2 > MaxLength)
                    break;
                result.Append(character).Append(value[++i]);
            }
            else
                result.Append(char.IsSurrogate(character) ? '\ufffd' : character);
        }
        return result.ToString();
    }
}
