namespace AzureSandboxDemo;

internal sealed class AzureSandboxSettings
{
    internal string SubscriptionId { get; set; } =
        Environment.GetEnvironmentVariable("ACA_SUBSCRIPTION") ?? "";

    internal string ResourceGroup { get; set; } =
        Environment.GetEnvironmentVariable("ACA_RESOURCE_GROUP") ?? "hex1b-sandbox-demo-rg";

    internal string Region { get; set; } =
        Environment.GetEnvironmentVariable("ACA_REGION") ?? "eastus2";

    internal string SandboxGroup { get; set; } =
        Environment.GetEnvironmentVariable("ACA_SANDBOX_GROUP") ?? "hex1b-sandbox-demo";

    internal string DiskImage { get; set; } = "ubuntu";

    internal string Command { get; set; } = "/bin/bash";

    internal string SandboxId { get; set; } = "";

    internal AzureSandboxSettings Snapshot()
        => new()
        {
            SubscriptionId = Require(nameof(SubscriptionId), SubscriptionId),
            ResourceGroup = Require(nameof(ResourceGroup), ResourceGroup),
            Region = Require(nameof(Region), Region),
            SandboxGroup = Require(nameof(SandboxGroup), SandboxGroup),
            DiskImage = Require(nameof(DiskImage), DiskImage),
            Command = Require(nameof(Command), Command),
            SandboxId = SandboxId.Trim()
        };

    internal AzureSandboxSettings SnapshotWithSandbox()
    {
        var snapshot = Snapshot();
        snapshot.SandboxId = Require(nameof(SandboxId), SandboxId);
        return snapshot;
    }

    private static string Require(string name, string value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} is required.")
            : value.Trim();
}
