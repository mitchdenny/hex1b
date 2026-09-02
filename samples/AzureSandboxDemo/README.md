# Azure Sandbox Demo

This sample creates an ephemeral [Azure Container Apps Sandbox](https://learn.microsoft.com/azure/container-apps/sandboxes-overview), starts `Hex1b.Tool` inside it, publishes the tool's WebSocket port behind Microsoft Entra authentication, and embeds the remote shell in a `TerminalWidget`.

When the remote shell exits, the sample removes the protected endpoint and deletes the sandbox. The UI then offers to create a fresh session. The shared Sandbox Group is never deleted.

> [!IMPORTANT]
> Azure Container Apps Sandboxes and the `aca` CLI are in preview. Command shapes and endpoint authentication may change.

## Prerequisites

1. Install the .NET 10 SDK and Azure CLI.
2. Install the preview `aca` CLI:

   ```bash
   curl -fsSL https://aka.ms/aca-cli-install | sh
   ```

3. Sign in:

   ```bash
   az login
   ```

4. Create an Azure Container Apps Sandbox Group and grant your signed-in identity the **Container Apps SandboxGroup Data Owner** role. See the [official CLI quickstart](https://learn.microsoft.com/azure/container-apps/sandboxes-quickstart-cli).
5. Ensure sandboxes in the group can reach NuGet when using the default setup. The sample installs `Hex1b.Tool` from NuGet into each fresh sandbox.

## Run the sample

```bash
dotnet run --project samples/AzureSandboxDemo -- \
  --resource-group <resource-group> \
  --sandbox-group <sandbox-group>
```

The active Azure CLI subscription, Sandbox Group region, and signed-in user's Microsoft Entra object ID are discovered automatically. Override them when needed:

```bash
dotnet run --project samples/AzureSandboxDemo -- \
  --subscription <subscription-id> \
  --resource-group <resource-group> \
  --sandbox-group <sandbox-group> \
  --region <region> \
  --principal-id <entra-object-id>
```

The default public sandbox disk is `dotnet`. Use `--disk <name>` for another public disk or `--disk-id <resource-id>` for a private disk image. Use `--tool-version <version>` to pin the `Hex1b.Tool` package.

## Prebaked disk image

For faster startup, build a Linux amd64 OCI image that already contains `Hex1b.Tool`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN dotnet tool install --global Hex1b.Tool \
    && ln -s /root/.dotnet/tools/hex1b /usr/local/bin/hex1b
```

Push the image to a registry available to the Sandbox Group, then convert it to a reusable disk:

```bash
aca sandboxgroup disk create \
  --name hex1b-dotnet10 \
  --image <registry>/hex1b-sandbox:<tag> \
  --output json
```

Run the sample with the returned disk resource ID:

```bash
dotnet run --project samples/AzureSandboxDemo -- \
  --resource-group <resource-group> \
  --sandbox-group <sandbox-group> \
  --disk-id <disk-resource-id> \
  --skip-tool-install
```

## Lifecycle

The sample performs these operations for each session:

1. Creates a sandbox with `aca sandbox create`.
2. Starts `hex1b terminal start --port 8080 --bind 0.0.0.0`.
3. Exposes port 8080 with `--auth entra --allow-principal`.
4. Gets an access token for `https://auth.adcproxy.io/` through `az account get-access-token`.
5. Connects `WithRemoteTerminal` to the returned `wss://` endpoint and supplies the token through `ClientWebSocketOptions`.
6. Removes the endpoint and deletes the sandbox when the shell exits or the application shuts down.

If cleanup fails, the UI retains the sandbox ID and offers a retry instead of silently abandoning the resource.
If the application exits while creation is in progress, shutdown waits for the create command to return the server-assigned sandbox ID before attempting deletion.
