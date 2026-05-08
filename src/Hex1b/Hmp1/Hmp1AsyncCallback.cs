namespace Hex1b;

/// <summary>
/// Internal helpers for invoking the async callback properties on the
/// HMP v1 surfaces (<see cref="IHmp1ConnectionHandle"/>,
/// <see cref="Hmp1ClientOptions"/>, <see cref="Hmp1ServerOptions"/>,
/// <see cref="Hmp1WorkloadAdapter"/>, <see cref="Hmp1PresentationAdapter"/>).
/// </summary>
/// <remarks>
/// <para>
/// Although the public surfaces expose callbacks as ordinary
/// <see cref="Func{T1, T2, TResult}"/> properties (not C# events), the
/// runtime delegate combine semantic still applies — assigning with
/// <c>+=</c> produces a multicast delegate. If we naively did
/// <c>await cb(args, ct)</c> on a multicast, only the *last* handler's
/// returned <see cref="Task"/> would be awaited; earlier handlers
/// would be invoked but their async work would be effectively
/// fire-and-forget (with their exceptions silently dropped). The
/// <c>InvokeAsync</c> helpers here iterate <see cref="Delegate.GetInvocationList"/>
/// and await each handler individually so multicast composition behaves
/// exactly as a developer migrating from C# events would expect.
/// </para>
/// <para>
/// Each handler invocation is wrapped in a <c>try</c> that swallows
/// <see cref="OperationCanceledException"/> and any other exception:
/// callbacks are observers and must not be able to break the read pump
/// or the per-client write pump that invoked them.
/// </para>
/// <para>
/// During invocation, <see cref="Hmp1CallbackContext.InCallback"/> is
/// set to <see langword="true"/> via an <see cref="AsyncLocal{T}"/>.
/// <see cref="Hmp1WorkloadAdapter.DisposeAsync"/> consults the flag so
/// a callback that calls back into the adapter
/// (<c>await connection.DisposeAsync()</c> from
/// <see cref="IHmp1ConnectionHandle.OnRoleChanged"/>, for example) does
/// not deadlock waiting for the read pump task it is currently running
/// on.
/// </para>
/// </remarks>
internal static class Hmp1AsyncCallback
{
    public static async Task InvokeAsync<TArgs>(
        Func<TArgs, CancellationToken, Task>? callback,
        TArgs args,
        CancellationToken ct)
    {
        if (callback is null)
        {
            return;
        }

        using var scope = Hmp1CallbackContext.Enter();
        foreach (var d in callback.GetInvocationList())
        {
            var handler = (Func<TArgs, CancellationToken, Task>)d;
            try
            {
                await handler(args, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Observer chose to honour cancellation; carry on with the
                // remaining observers.
            }
            catch
            {
                // Observer threw; isolate the failure so it cannot break
                // the pump that invoked us. By design — the alternative
                // (propagating) tears down the connection on a stray
                // observer bug.
            }
        }
    }

    public static async Task InvokeAsync(
        Func<CancellationToken, Task>? callback,
        CancellationToken ct)
    {
        if (callback is null)
        {
            return;
        }

        using var scope = Hmp1CallbackContext.Enter();
        foreach (var d in callback.GetInvocationList())
        {
            var handler = (Func<CancellationToken, Task>)d;
            try
            {
                await handler(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }
    }
}

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
