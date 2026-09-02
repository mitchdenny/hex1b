using System.Text.Json;

namespace AzureSandboxDemo;

internal sealed class AzureSandboxClient(
    AzureSandboxDemoOptions options,
    CommandRunner commandRunner)
{
    private string? _subscriptionId;
    private string? _region;
    private string? _principalId;

    public async Task InitializeAsync(
        Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        reportProgress("Checking Azure Container Apps Sandbox CLI...");
        await commandRunner.RunAsync("aca", ["--version"], cancellationToken);

        reportProgress("Resolving Azure subscription...");
        _subscriptionId = options.SubscriptionId;
        if (string.IsNullOrWhiteSpace(_subscriptionId))
        {
            var account = await commandRunner.RunAsync(
                "az",
                ["account", "show", "--query", "id", "--output", "tsv"],
                cancellationToken);
            _subscriptionId = RequireValue(account.StandardOutput, "Azure subscription ID");
        }

        reportProgress("Resolving Sandbox Group region...");
        _region = options.Region;
        if (string.IsNullOrWhiteSpace(_region))
        {
            var group = await commandRunner.RunAsync(
                "az",
                [
                    "resource", "show",
                    "--subscription", _subscriptionId,
                    "--resource-group", options.ResourceGroup,
                    "--resource-type", "Microsoft.App/sandboxGroups",
                    "--name", options.SandboxGroup,
                    "--query", "location",
                    "--output", "tsv"
                ],
                cancellationToken);
            _region = RequireValue(group.StandardOutput, "Sandbox Group region");
        }

        reportProgress("Resolving Microsoft Entra principal...");
        _principalId = options.PrincipalId;
        if (string.IsNullOrWhiteSpace(_principalId))
        {
            var principal = await commandRunner.RunAsync(
                "az",
                ["ad", "signed-in-user", "show", "--query", "id", "--output", "tsv"],
                cancellationToken);
            _principalId = RequireValue(principal.StandardOutput, "signed-in user object ID");
        }
    }

    public async Task<string> CreateSandboxAsync(CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "sandbox", "create"
        };

        if (options.DiskId is not null)
            arguments.AddRange(["--disk-id", options.DiskId]);
        else
            arguments.AddRange(["--disk", options.Disk!]);

        arguments.AddRange(
        [
            "--label", "app=hex1b-remote-terminal",
            "--label", $"session={Guid.NewGuid():N}",
            "--output", "json"
        ]);
        AddSandboxScope(arguments);

        var result = await commandRunner.RunAsync("aca", arguments, cancellationToken);
        return ReadRequiredJsonString(result.StandardOutput, "id", "sandbox create");
    }

    public async Task StartHex1bHostAsync(
        string sandboxId,
        CancellationToken cancellationToken)
    {
        var versionArgument = options.Hex1bToolVersion is null
            ? ""
            : $" --version '{options.Hex1bToolVersion}'";
        var installCommand = options.SkipToolInstall
            ? "command -v hex1b >/dev/null"
            : $"command -v hex1b >/dev/null || dotnet tool install --global Hex1b.Tool{versionArgument}";

        var command =
            $"""
            set -eu
            export PATH="$PATH:$HOME/.dotnet/tools"
            {installCommand}
            nohup hex1b terminal start \
              --port {options.Port} \
              --bind 0.0.0.0 \
              --width 120 \
              --height 30 \
              -- bash --norc \
              > /tmp/hex1b-tool.log 2>&1 < /dev/null &
            hex1b_pid=$!
            attempt=0
            while [ "$attempt" -lt 60 ]; do
              if ! kill -0 "$hex1b_pid" 2>/dev/null; then
                cat /tmp/hex1b-tool.log >&2
                exit 1
              fi
              if bash -c "exec 3<>/dev/tcp/127.0.0.1/{options.Port}" 2>/dev/null; then
                exit 0
              fi
              attempt=$((attempt + 1))
              sleep 1
            done
            cat /tmp/hex1b-tool.log >&2
            exit 1
            """;

        var arguments = new List<string>
        {
            "sandbox", "exec",
            "--id", sandboxId,
            "-c", command
        };
        AddSandboxScope(arguments);
        await commandRunner.RunAsync("aca", arguments, cancellationToken);
    }

    public async Task<Uri> ExposeProtectedPortAsync(
        string sandboxId,
        Action portExposed,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();

        var arguments = new List<string>
        {
            "sandbox", "port", "add",
            "--id", sandboxId,
            "--port", options.Port.ToString(),
            "--auth", "entra",
            "--allow-principal", _principalId!,
            "--output", "json"
        };
        AddSandboxScope(arguments);

        var result = await commandRunner.RunAsync("aca", arguments, cancellationToken);
        portExposed();
        var endpoint = ReadRequiredJsonString(result.StandardOutput, "url", "sandbox port add");

        try
        {
            var uriBuilder = new UriBuilder(endpoint)
            {
                Scheme = Uri.UriSchemeWss,
                Path = $"{new Uri(endpoint).AbsolutePath.TrimEnd('/')}/ws/attach"
            };
            return uriBuilder.Uri;
        }
        catch (UriFormatException exception)
        {
            throw new AzureSandboxException(
                $"'aca sandbox port add' returned an invalid URL: {endpoint}",
                exception);
        }
    }

    public async Task<string> GetProxyAccessTokenAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();

        var result = await commandRunner.RunAsync(
            "az",
            [
                "account", "get-access-token",
                "--subscription", _subscriptionId!,
                "--resource", AzureSandboxDemoOptions.ProxyTokenResource,
                "--query", "accessToken",
                "--output", "tsv"
            ],
            cancellationToken,
            includeFailureOutput: false);

        return RequireValue(result.StandardOutput, "ADC proxy access token");
    }

    public async Task CleanupSandboxAsync(
        string sandboxId,
        bool portWasExposed,
        Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        AzureSandboxException? removePortError = null;
        if (portWasExposed)
        {
            reportProgress("Removing the protected endpoint...");
            var removeArguments = new List<string>
            {
                "sandbox", "port", "remove",
                "--id", sandboxId,
                "--port", options.Port.ToString()
            };
            AddSandboxScope(removeArguments);

            try
            {
                await commandRunner.RunAsync("aca", removeArguments, cancellationToken);
            }
            catch (AzureSandboxException exception)
            {
                removePortError = exception;
                reportProgress("Endpoint removal failed; sandbox deletion will still be attempted.");
            }
        }

        reportProgress($"Deleting sandbox {ShortId(sandboxId)}...");
        var deleteArguments = new List<string>
        {
            "sandbox", "delete",
            "--id", sandboxId,
            "--yes"
        };
        AddSandboxScope(deleteArguments);

        try
        {
            await commandRunner.RunAsync("aca", deleteArguments, cancellationToken);
        }
        catch (AzureSandboxException deleteError) when (removePortError is not null)
        {
            throw new AzureSandboxException(
                $"{removePortError.Message} Sandbox deletion also failed: {deleteError.Message}",
                deleteError);
        }
    }

    private void AddSandboxScope(List<string> arguments)
    {
        EnsureInitialized();
        arguments.AddRange(
        [
            "--subscription", _subscriptionId!,
            "--resource-group", options.ResourceGroup,
            "--sandbox-group", options.SandboxGroup,
            "--region", _region!
        ]);
    }

    private void EnsureInitialized()
    {
        if (_subscriptionId is null || _region is null || _principalId is null)
            throw new InvalidOperationException("The Azure Sandbox client has not been initialized.");
    }

    private static string ReadRequiredJsonString(
        string json,
        string propertyName,
        string operation)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                property.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }
        catch (JsonException exception)
        {
            throw new AzureSandboxException(
                $"'aca {operation}' returned invalid JSON.",
                exception);
        }

        throw new AzureSandboxException(
            $"'aca {operation}' output did not contain a non-empty '{propertyName}' property.");
    }

    private static string RequireValue(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AzureSandboxException($"Could not resolve {description}.");
        return value.Trim();
    }

    private static string ShortId(string sandboxId)
        => sandboxId.Length <= 8 ? sandboxId : sandboxId[..8];
}
