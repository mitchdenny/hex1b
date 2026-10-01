namespace Hex1b;

/// <summary>
/// Tracks whether the current async context is executing inside an
/// HMP v1 callback dispatch. Used by
/// <see cref="Hmp1WorkloadAdapter.DisposeAsync"/> to avoid the
/// self-deadlock when a callback calls back into the adapter.
/// </summary>
internal static class Hmp1CallbackContext
{
    private static readonly AsyncLocal<bool> s_inCallback = new();

    public static bool InCallback => s_inCallback.Value;

    public static Scope Enter()
    {
        var previous = s_inCallback.Value;
        s_inCallback.Value = true;
        return new Scope(previous);
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly bool _previous;

        public Scope(bool previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            s_inCallback.Value = _previous;
        }
    }
}
