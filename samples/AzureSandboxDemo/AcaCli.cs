using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AzureSandboxDemo;

internal sealed class AcaCli
{
    private static readonly TimeSpan DataPlaneAccessTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DataPlaneAccessRetryDelay = TimeSpan.FromSeconds(5);

    internal async Task<string> GetActiveSubscriptionIdAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "az",
            ["account", "show", "--query", "id", "--output", "tsv"],
            cancellationToken);
        return RequireValue(result.StandardOutput, "Azure CLI returned no active subscription.");
    }

    internal async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync("aca", ["--version"], cancellationToken);
        return RequireValue(result.StandardOutput, "ACA CLI returned no version.");
    }

    internal async Task CreateSandboxGroupAsync(
        AzureSandboxSettings settings,
        Action<string> reportStatus,
        CancellationToken cancellationToken)
    {
        reportStatus("Creating or updating the Azure resource group...");
        await RunAsync(
            "az",
            [
                "group", "create",
                "--name", settings.ResourceGroup,
                "--location", settings.Region,
                "--subscription", settings.SubscriptionId,
                "--output", "none"
            ],
            cancellationToken);

        reportStatus("Creating the sandbox group and assigning Data Owner access...");
        await RunAsync(
            "aca",
            [
                "sandboxgroup", "create",
                "--name", settings.SandboxGroup,
                "--location", settings.Region,
                "--subscription", settings.SubscriptionId,
                "--resource-group", settings.ResourceGroup,
                "--output", "json"
            ],
            cancellationToken);

        await WaitForDataPlaneAccessAsync(settings, reportStatus, cancellationToken);
    }

    internal async Task WaitForDataPlaneAccessAsync(
        AzureSandboxSettings settings,
        Action<string> reportStatus,
        CancellationToken cancellationToken)
    {
        reportStatus("Checking ACA data-plane access...");
        var deadline = DateTimeOffset.UtcNow + DataPlaneAccessTimeout;

        while (true)
        {
            try
            {
                await RunAsync(
                    "aca",
                    [
                        "sandbox", "list",
                        "--group", settings.SandboxGroup,
                        "--subscription", settings.SubscriptionId,
                        "--resource-group", settings.ResourceGroup,
                        "--region", settings.Region,
                        "--output", "json"
                    ],
                    cancellationToken);
                return;
            }
            catch (AcaCliCommandException ex) when (ex.IsForbidden)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new InvalidOperationException(
                        "ACA data-plane access is still forbidden after waiting for role propagation. " +
                        "Verify that the signed-in user has the Container Apps SandboxGroup Data Owner " +
                        "role on the sandbox group.",
                        ex);
                }

                reportStatus("Waiting for Container Apps SandboxGroup Data Owner role propagation...");
                await Task.Delay(DataPlaneAccessRetryDelay, cancellationToken);
            }
        }
    }

    internal async Task<string> CreateSandboxAsync(
        AzureSandboxSettings settings,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "aca",
            [
                "sandbox", "create",
                "--group", settings.SandboxGroup,
                "--disk", settings.DiskImage,
                "--subscription", settings.SubscriptionId,
                "--resource-group", settings.ResourceGroup,
                "--region", settings.Region,
                "--output", "json"
            ],
            cancellationToken);

        if (TryGetSandboxId(result.StandardOutput, out var id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"ACA CLI sandbox creation response did not contain an id: {Normalize(result.StandardOutput)}");
    }

    internal Task DeleteSandboxAsync(
        AzureSandboxSettings settings,
        CancellationToken cancellationToken)
        => RunAsync(
            "aca",
            [
                "sandbox", "delete",
                "--group", settings.SandboxGroup,
                "--id", settings.SandboxId,
                "--yes",
                "--subscription", settings.SubscriptionId,
                "--resource-group", settings.ResourceGroup,
                "--region", settings.Region,
                "--output", "json"
            ],
            cancellationToken);

    internal Task DeleteSandboxGroupAsync(
        AzureSandboxSettings settings,
        CancellationToken cancellationToken)
        => RunAsync(
            "aca",
            [
                "sandboxgroup", "delete",
                "--name", settings.SandboxGroup,
                "--yes",
                "--subscription", settings.SubscriptionId,
                "--resource-group", settings.ResourceGroup,
                "--output", "json"
            ],
            cancellationToken);

    internal async Task<string> GetDataPlaneTokenAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "az",
            [
                "account", "get-access-token",
                "--subscription", subscriptionId,
                "--scope", AzureSandboxWorkloadAdapter.DefaultTokenScope,
                "--query", "accessToken",
                "--output", "tsv"
            ],
            cancellationToken);
        return RequireValue(result.StandardOutput, "Azure CLI returned no data-plane access token.");
    }

    private static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"'{fileName}' was not found. Install it and ensure it is available on PATH.",
                ex);
        }

        using (process)
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                throw;
            }

            var output = await standardOutput;
            var error = await standardError;
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new AcaCliCommandException(fileName, process.ExitCode, detail);
            }

            return new ProcessResult(output.Trim(), error.Trim());
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            property.GetString() is { Length: > 0 } text)
        {
            value = text;
            return true;
        }

        value = "";
        return false;
    }

    private static bool TryGetSandboxId(string output, out string id)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            if (TryGetString(document.RootElement, "id", out id) ||
                TryGetString(document.RootElement, "sandboxId", out id))
            {
                return true;
            }
        }
        catch (JsonException)
        {
        }

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "Created sandbox:";
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = trimmed[prefix.Length..].Trim();
                if (Guid.TryParse(candidate, out var sandboxId))
                {
                    id = sandboxId.ToString();
                    return true;
                }
            }
        }

        id = "";
        return false;
    }

    private static string RequireValue(string value, string error)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(error)
            : value.Trim();

    private static string Normalize(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 500 ? normalized : $"{normalized[..500]}...";
    }

    private sealed record ProcessResult(string StandardOutput, string StandardError);

    private sealed class AcaCliCommandException : InvalidOperationException
    {
        internal AcaCliCommandException(string command, int exitCode, string detail)
            : base($"{command} exited with code {exitCode}: {Normalize(detail)}")
        {
            IsForbidden =
                detail.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("forbidden", StringComparison.OrdinalIgnoreCase);
        }

        internal bool IsForbidden { get; }
    }
}
