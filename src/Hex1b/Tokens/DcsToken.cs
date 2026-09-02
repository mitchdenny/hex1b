using System.Runtime.CompilerServices;

namespace Hex1b.Tokens;

/// <summary>
/// Represents a Device Control String (DCS): ESC P ... ST
/// </summary>
/// <param name="Payload">
/// The DCS content between the ESC P introducer and ST terminator.
/// </param>
/// <remarks>
/// <para>
/// The most common DCS use in Hex1b is Sixel graphics (ESC P q ... ST).
/// We preserve the entire payload because:
/// <list type="bullet">
///   <item>Sixel data can be large and complex</item>
///   <item>Filters typically pass through or drop entirely</item>
///   <item>Re-parsing would be wasteful</item>
/// </list>
/// </para>
/// </remarks>
public sealed record DcsToken(string Payload) : AnsiToken
{
    private static readonly ConditionalWeakTable<DcsToken, RawPayloadHolder> s_rawPayloads = new();

    private DcsToken(DcsToken original) : base(original)
    {
        Payload = original.Payload;
        if (s_rawPayloads.TryGetValue(original, out var holder))
        {
            s_rawPayloads.Add(this, holder);
        }
    }

    internal void AttachRawPayload(ReadOnlyMemory<byte> rawPayload) =>
        s_rawPayloads.Add(this, new RawPayloadHolder(rawPayload));

    internal bool TryGetMatchingRawPayload(out ReadOnlyMemory<byte> rawPayload)
    {
        if (!s_rawPayloads.TryGetValue(this, out var holder))
        {
            rawPayload = default;
            return false;
        }

        rawPayload = holder.Payload;
        if (rawPayload.Length != Payload.Length)
        {
            return false;
        }

        for (var index = 0; index < Payload.Length; index++)
        {
            if (Payload[index] > byte.MaxValue ||
                (byte)Payload[index] != rawPayload.Span[index])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record RawPayloadHolder(ReadOnlyMemory<byte> Payload);
}
