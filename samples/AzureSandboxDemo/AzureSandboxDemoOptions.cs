namespace AzureSandboxDemo;

internal sealed record AzureSandboxDemoOptions(
    string ResourceGroup,
    string SandboxGroup,
    string? SubscriptionId,
    string? Region,
    string? Disk,
    string? DiskId,
    string? Hex1bToolVersion,
    string? PrincipalId,
    int Port,
    bool SkipToolInstall)
{
    public const string ProxyTokenResource = "https://auth.adcproxy.io/";

    public const string Usage =
        """
        AzureSandboxDemo

        Usage:
          dotnet run --project samples/AzureSandboxDemo -- \
            --resource-group <name> \
            --sandbox-group <name> \
            [--subscription <id>] \
            [--region <azure-region>] \
            [--disk <public-disk-name> | --disk-id <resource-id>] \
            [--tool-version <version>] \
            [--principal-id <entra-object-id>] \
            [--port <port>] \
            [--skip-tool-install]

        The default public disk is "dotnet" and the default port is 8080.
        Subscription, region, and the signed-in user's object ID are discovered
        through Azure CLI when omitted.
        """;

    public static AzureSandboxDemoOptions? Parse(string[] args, out string? error)
    {
        error = null;
        string? resourceGroup = null;
        string? sandboxGroup = null;
        string? subscriptionId = null;
        string? region = null;
        string? disk = "dotnet";
        string? diskId = null;
        string? toolVersion = null;
        string? principalId = null;
        var port = 8080;
        var skipToolInstall = false;
        var diskSpecified = false;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            switch (argument)
            {
                case "--help" or "-h":
                    return null;
                case "--resource-group":
                    if (!TryReadValue(args, ref i, argument, out resourceGroup, out error))
                        return null;
                    break;
                case "--sandbox-group":
                    if (!TryReadValue(args, ref i, argument, out sandboxGroup, out error))
                        return null;
                    break;
                case "--subscription":
                    if (!TryReadValue(args, ref i, argument, out subscriptionId, out error))
                        return null;
                    break;
                case "--region":
                    if (!TryReadValue(args, ref i, argument, out region, out error))
                        return null;
                    break;
                case "--disk":
                    if (diskId is not null)
                    {
                        error = "--disk and --disk-id cannot be used together.";
                        return null;
                    }
                    if (!TryReadValue(args, ref i, argument, out disk, out error))
                        return null;
                    diskSpecified = true;
                    break;
                case "--disk-id":
                    if (diskSpecified)
                    {
                        error = "--disk and --disk-id cannot be used together.";
                        return null;
                    }
                    if (!TryReadValue(args, ref i, argument, out diskId, out error))
                        return null;
                    disk = null;
                    break;
                case "--tool-version":
                    if (!TryReadValue(args, ref i, argument, out toolVersion, out error))
                        return null;
                    break;
                case "--principal-id":
                    if (!TryReadValue(args, ref i, argument, out principalId, out error))
                        return null;
                    break;
                case "--port":
                    if (!TryReadValue(args, ref i, argument, out var portValue, out error))
                        return null;
                    if (!int.TryParse(portValue, out port) || port is < 1 or > 65535)
                    {
                        error = "--port must be an integer from 1 through 65535.";
                        return null;
                    }
                    break;
                case "--skip-tool-install":
                    skipToolInstall = true;
                    break;
                default:
                    error = $"Unknown argument: {argument}";
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(resourceGroup))
        {
            error = "--resource-group is required.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(sandboxGroup))
        {
            error = "--sandbox-group is required.";
            return null;
        }

        if (toolVersion is not null &&
            toolVersion.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '-' and not '+' and not '*'))
        {
            error = "--tool-version contains unsupported characters.";
            return null;
        }

        return new AzureSandboxDemoOptions(
            resourceGroup,
            sandboxGroup,
            subscriptionId,
            region,
            disk,
            diskId,
            toolVersion,
            principalId,
            port,
            skipToolInstall);
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string argument,
        out string? value,
        out string? error)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            value = null;
            error = $"{argument} requires a value.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }
}
