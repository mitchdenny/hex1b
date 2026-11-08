#if !NET9_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

// .NET 9+ trim/AOT tools recognize this attribute by its full name, including
// when it is defined in a library targeting an older framework.
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class FeatureSwitchDefinitionAttribute(string switchName) : Attribute
{
    public string SwitchName { get; } = switchName;
}
#endif
