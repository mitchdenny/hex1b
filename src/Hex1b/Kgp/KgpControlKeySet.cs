namespace Hex1b;

internal readonly record struct KgpControlKeySet(ulong Bits)
{
    internal bool Contains(char key)
        => (Bits & GetBit(key)) != 0;

    internal bool IsSubsetOf(KgpControlKeySet other)
        => (Bits & ~other.Bits) == 0;

    internal KgpControlKeySet Add(char key)
        => new(Bits | GetBit(key));

    internal static KgpControlKeySet From(char key)
        => new(GetBit(key));

    private static ulong GetBit(char key)
    {
        var index = key switch
        {
            >= 'a' and <= 'z' => key - 'a',
            >= 'A' and <= 'Z' => 26 + key - 'A',
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

        return 1UL << index;
    }
}
