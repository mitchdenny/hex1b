using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Hex1b.Tokens;

/// <summary>
/// Internal pool of frequently-emitted token instances so the renderer hot path
/// can reuse <see cref="SgrToken"/> / <see cref="TextToken"/> values instead of
/// allocating a fresh record per cell every frame.
/// </summary>
/// <remarks>
/// <para>
/// Tokens are <see langword="public sealed record"/> types with value-based equality
/// and no mutable state, so two callers receiving the same cached instance behave
/// identically to two callers receiving distinct equal instances. The caches are
/// bounded to avoid unbounded growth from pathological workloads; on overflow the
/// fallback path simply allocates a fresh record.
/// </para>
/// <para>
/// The SGR cache is keyed by a hash of the assembled <see cref="StringBuilder"/>
/// contents with a span-vs-string equality verification on lookup, so cache hits
/// never materialise the SGR text into a heap string. <c>GetAlternateLookup</c>
/// would be a cleaner API but is .NET 9+ only and this lib targets net8.0.
/// </para>
/// <para>
/// Per-thread state (<see cref="ThreadStaticAttribute">thread-static</see>) avoids
/// lock contention when multiple Hex1bApp instances render concurrently in tests.
/// </para>
/// </remarks>
internal static class AnsiTokenCache
{
    [ThreadStatic] private static StringBuilder? t_sgrBuilder;
    [ThreadStatic] private static Dictionary<ulong, SgrEntry>? t_sgrCache;
    [ThreadStatic] private static Dictionary<ulong, SgrEntry>? t_sgrBytesCache;

    private static readonly ConcurrentDictionary<string, TextToken> s_textCache = new();

    private const int MaxSgrCache = 4096;
    private const int MaxTextCache = 2048;

    private readonly struct SgrEntry
    {
        public readonly string Parameters;
        public readonly SgrToken Token;
        public SgrEntry(string parameters, SgrToken token)
        {
            Parameters = parameters;
            Token = token;
        }
    }

    /// <summary>
    /// Returns a cleared <see cref="StringBuilder"/> for assembling SGR parameter
    /// strings on the current thread.
    /// </summary>
    public static StringBuilder GetSgrBuilder()
    {
        var sb = t_sgrBuilder ??= new StringBuilder(64);
        sb.Clear();
        return sb;
    }

    /// <summary>
    /// Looks up a cached <see cref="SgrToken"/> matching the assembled contents of
    /// <paramref name="builder"/>, allocating one (and the backing string) only on
    /// cache miss. Lookup hashes the builder contents and verifies span equality
    /// against the stored key without materialising a string on the hot path.
    /// </summary>
    public static SgrToken GetSgrToken(StringBuilder builder)
    {
        if (builder.Length == 0)
            return SgrToken.Reset;

        var cache = t_sgrCache ??= new Dictionary<ulong, SgrEntry>(capacity: 256);
        var hash = HashBuilder(builder);

        if (cache.TryGetValue(hash, out var entry) && BuilderEqualsString(builder, entry.Parameters))
            return entry.Token;

        // Cache miss (or once-in-a-blue-moon hash collision) — materialise the string.
        var parameters = builder.ToString();
        var token = new SgrToken(parameters);
        if (cache.Count < MaxSgrCache)
            cache[hash] = new SgrEntry(parameters, token);
        return token;
    }

    /// <summary>
    /// Returns a cached <see cref="TextToken"/> for the given text, allocating
    /// one on cache miss. Short strings (single graphemes — the common case for
    /// the per-cell text emission path) are cached aggressively; long strings
    /// bypass the cache entirely since they're unlikely to recur.
    /// </summary>
    public static TextToken GetTextToken(string text)
    {
        if (text.Length > 8)
            return new TextToken(text);

        if (s_textCache.TryGetValue(text, out var token))
            return token;

        token = new TextToken(text);
        if (s_textCache.Count < MaxTextCache)
            s_textCache.TryAdd(text, token);
        return token;
    }

    // FNV-1a 64-bit hash over the builder's chars. Cheap, allocation-free, good
    // collision resistance for short SGR strings (<128 chars).
    private static ulong HashBuilder(StringBuilder builder)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        ulong hash = fnvOffset;
        var len = builder.Length;
        for (var i = 0; i < len; i++)
        {
            var ch = builder[i];
            hash ^= ch;
            hash *= fnvPrime;
        }
        return hash;
    }

    /// <summary>
    /// Returns a cached <see cref="SgrToken"/> whose <see cref="SgrToken.PreformattedBytes"/>
    /// is the wire-ready UTF-8 representation of the SGR parameters (without the
    /// surrounding <c>ESC[</c> ... <c>m</c>). Lookup is keyed by an FNV-1a hash
    /// over <paramref name="parameterBytes"/> with a span-vs-array equality verify
    /// on lookup, so cache hits allocate nothing.
    /// </summary>
    /// <remarks>
    /// On miss this copies the bytes into a heap byte[] and materialises an ASCII
    /// string for <see cref="SgrToken.Parameters"/> (one-time cost; cache hits skip
    /// both). SGR parameters are pure ASCII (digits, semicolons, colons), so ASCII
    /// decode is exact.
    /// </remarks>
    public static SgrToken GetSgrTokenFromBytes(ReadOnlySpan<byte> parameterBytes)
    {
        if (parameterBytes.Length == 0)
            return SgrToken.Reset;

        var cache = t_sgrBytesCache ??= new Dictionary<ulong, SgrEntry>(capacity: 256);
        var hash = HashBytes(parameterBytes);

        if (cache.TryGetValue(hash, out var entry) && BytesEqualsBytes(parameterBytes, entry.Token.PreformattedBytes))
            return entry.Token;

        // Cache miss — materialise the wire bytes + the parameter string.
        var bytes = parameterBytes.ToArray();
        var parameters = System.Text.Encoding.ASCII.GetString(bytes);
        var token = new SgrToken(parameters) { PreformattedBytes = bytes };
        if (cache.Count < MaxSgrCache)
            cache[hash] = new SgrEntry(parameters, token);
        return token;
    }

    // FNV-1a 64-bit hash over a byte span.
    private static ulong HashBytes(ReadOnlySpan<byte> data)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        ulong hash = fnvOffset;
        for (var i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= fnvPrime;
        }
        return hash;
    }

    private static bool BytesEqualsBytes(ReadOnlySpan<byte> a, byte[]? b)
    {
        if (b is null) return false;
        return a.SequenceEqual(b);
    }

    private static bool BuilderEqualsString(StringBuilder builder, string s)
    {
        if (builder.Length != s.Length) return false;
        for (var i = 0; i < builder.Length; i++)
        {
            if (builder[i] != s[i]) return false;
        }
        return true;
    }
}


