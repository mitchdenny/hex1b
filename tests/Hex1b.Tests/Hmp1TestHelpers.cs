using Hex1b;

namespace Hex1b.Tests;

/// <summary>
/// Test-only helpers absorbing the Phase 15 ctor migration. Tests that
/// used to call <c>new Hmp1WorkloadAdapter(stream, ...)</c> can call
/// <see cref="NewClient"/> instead while the new public ctor takes
/// <see cref="Hmp1ClientOptions"/>. Production callers should pass
/// options directly or use the <c>WithHmp1*</c> builder extensions.
/// </summary>
internal static class Hmp1TestHelpers
{
    internal static Hmp1WorkloadAdapter NewClient(
        Stream stream,
        string? displayName = null,
        Hmp1Role? defaultRole = null)
    {
        return new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(stream),
            DisplayName = displayName,
            DefaultRole = defaultRole,
        });
    }
}
