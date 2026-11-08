using System.Diagnostics.CodeAnalysis;

namespace Hex1b;

internal static class SurfacePooling
{
    [FeatureSwitchDefinition("Hex1b.SurfacePooling.IsSupported")]
    internal static bool IsSupported =>
        !AppContext.TryGetSwitch("Hex1b.SurfacePooling.IsSupported", out var enabled) || enabled;
}
