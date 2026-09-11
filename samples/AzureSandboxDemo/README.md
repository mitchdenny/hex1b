# Azure Sandbox Demo

This sample provisions Azure Container Apps sandboxes with the official `aca`
CLI and connects a Hex1b `TerminalWidget` directly to the sandbox PTY WebSocket.
The sample-local `AzureSandboxWorkloadAdapter` implements the ACA exec-stream
framing rather than launching `aca sandbox shell`.

## Prerequisites

1. Install the Azure CLI and sign in with `az login`.
2. Install the preview `aca` CLI:

   ```bash
   curl -fsSL https://aka.ms/aca-cli-install | sh
   ```

3. Run the sample:

   ```bash
   dotnet run --project samples/AzureSandboxDemo
   ```

The **Create group** action creates the resource group when necessary, creates
the sandbox group, and lets `aca` assign the signed-in user the Container Apps
SandboxGroup Data Owner role. **Create sandbox + connect** creates an Ubuntu
sandbox and opens `/bin/bash` through the WebSocket protocol.

Sandboxes and sandbox groups are Azure resources that may incur charges. Closing
the demo only disconnects the shell; use the delete actions in the UI when the
resources are no longer needed.
