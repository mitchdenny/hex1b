using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

public sealed class NativeAotEnabledAttribute : ConditionBaseAttribute
{
    public const string EnvironmentVariable = "HEX1B_RUN_NATIVE_AOT_TESTS";

    public NativeAotEnabledAttribute() : base(ConditionMode.Include)
    {
        IgnoreMessage = $"Native AOT scenarios are opt-in locally. Set {EnvironmentVariable}=1 to enable.";
    }

    public override bool IsConditionMet => IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariable));
    public override string GroupName => nameof(NativeAotEnabledAttribute);

    internal static bool IsEnabled(string? value) => value == "1";
}
