using System.Net.WebSockets;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace AzureSandboxDemo;

internal sealed class AzureSandboxDemoController(
    AzureSandboxDemoOptions options,
    AzureSandboxClient sandboxClient) : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Hex1bApp? _app;
    private Task? _lifecycleTask;
    private ActiveSandboxSession? _activeSession;
    private string? _sandboxId;
    private bool _portWasExposed;
    private AzureSandboxDemoStatus _status = AzureSandboxDemoStatus.Ready;
    private string _message = "Ready to create an Azure Container Apps sandbox.";

    public void Attach(Hex1bApp app)
    {
        _app = app;
    }

    public Hex1bWidget Build(RootContext context)
    {
        AzureSandboxDemoStatus status;
        string message;
        ActiveSandboxSession? activeSession;
        string? sandboxId;

        lock (_sync)
        {
            status = _status;
            message = _message;
            activeSession = _activeSession;
            sandboxId = _sandboxId;
        }

        return context.VStack(v =>
        [
            v.Text("Azure Sandbox Remote Terminal"),
            v.Separator(),
            activeSession is not null
                ? BuildTerminal(v, activeSession)
                : BuildStatus(v, status, message, sandboxId),
            v.InfoBar([
                "Ctrl+C", "Quit",
                "", message
            ])
        ]);
    }

    public void StartSandbox()
    {
        lock (_sync)
        {
            if (_lifecycleTask is { IsCompleted: false } || _sandboxId is not null)
                return;

            _status = AzureSandboxDemoStatus.Provisioning;
            _message = "Starting sandbox provisioning...";
            _lifecycleTask = RunSandboxLifecycleAsync();
        }

        Invalidate();
    }

    public void RetryCleanup()
    {
        lock (_sync)
        {
            if (_lifecycleTask is { IsCompleted: false } || _sandboxId is null)
                return;

            _status = AzureSandboxDemoStatus.CleaningUp;
            _message = "Retrying sandbox cleanup...";
            _lifecycleTask = RetryCleanupAsync();
        }

        Invalidate();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync();

        Task? lifecycleTask;
        lock (_sync)
            lifecycleTask = _lifecycleTask;

        try
        {
            if (lifecycleTask is not null)
                await lifecycleTask;
        }
        finally
        {
            string? remainingSandboxId;
            bool portWasExposed;
            lock (_sync)
            {
                remainingSandboxId = _sandboxId;
                portWasExposed = _portWasExposed;
            }

            try
            {
                if (remainingSandboxId is not null)
                {
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    try
                    {
                        await sandboxClient.CleanupSandboxAsync(
                            remainingSandboxId,
                            portWasExposed,
                            _ => { },
                            cleanupCts.Token);
                    }
                    catch (AzureSandboxException exception)
                    {
                        WriteCleanupFailure(remainingSandboxId, exception.Message);
                    }
                    catch (OperationCanceledException)
                    {
                        WriteCleanupFailure(remainingSandboxId, "The final cleanup attempt timed out.");
                    }
                }
            }
            finally
            {
                _lifetimeCts.Dispose();
            }
        }
    }

    private Hex1bWidget BuildTerminal(
        WidgetContext<VStackWidget> context,
        ActiveSandboxSession session)
        => context.Border(
                context.Terminal(session.Handle)
                    .WhenNotRunning(_ =>
                        context.Align(
                            Alignment.Center,
                            context.HStack(row =>
                            [
                                row.Spinner(),
                                row.Text(" Remote terminal exited; deleting sandbox...")
                            ])))
                    .Fill())
            .Title($"Sandbox {ShortId(session.SandboxId)}")
            .Fill();

    private Hex1bWidget BuildStatus(
        WidgetContext<VStackWidget> context,
        AzureSandboxDemoStatus status,
        string message,
        string? sandboxId)
    {
        var action = status switch
        {
            AzureSandboxDemoStatus.Ready or AzureSandboxDemoStatus.Failed
                => context.Button("Create sandbox").OnClick(_ => StartSandbox()),
            AzureSandboxDemoStatus.CleanupFailed
                => context.Button("Retry cleanup").OnClick(_ => RetryCleanup()),
            _ => null
        };

        var statusContent = new List<Hex1bWidget>
        {
            context.Text($"Sandbox Group: {options.SandboxGroup}"),
            context.Text($"Resource Group: {options.ResourceGroup}"),
            context.Text($"State: {StatusLabel(status)}"),
            context.Text("")
        };

        if (status is AzureSandboxDemoStatus.Provisioning or AzureSandboxDemoStatus.CleaningUp)
        {
            statusContent.Add(context.HStack(row =>
            [
                row.Spinner(),
                row.Text($" {message}")
            ]));
        }
        else
        {
            statusContent.Add(context.Text(message).Wrap());
        }

        if (sandboxId is not null)
        {
            statusContent.Add(context.Text(""));
            statusContent.Add(context.Text($"Sandbox ID: {sandboxId}").Wrap());
        }

        if (action is not null)
        {
            statusContent.Add(context.Text(""));
            statusContent.Add(action);
        }

        return context.Align(
                Alignment.Center,
                context.Border(context.VStack(_ => statusContent.ToArray()))
                    .Title("Azure Container Apps Sandboxes")
                    .FixedWidth(72))
            .Fill();
    }

    private async Task RunSandboxLifecycleAsync()
    {
        string? failure = null;
        ActiveSandboxSession? session = null;

        try
        {
            await sandboxClient.InitializeAsync(ReportProvisioning, _lifetimeCts.Token);

            ReportProvisioning("Creating sandbox...");
            // Let creation return its server-assigned ID during shutdown so the
            // lifecycle can delete a sandbox whose request reached Azure.
            var sandboxId = await sandboxClient.CreateSandboxAsync(CancellationToken.None);
            lock (_sync)
                _sandboxId = sandboxId;

            ReportProvisioning("Installing and starting Hex1b.Tool...");
            await sandboxClient.StartHex1bHostAsync(sandboxId, _lifetimeCts.Token);

            ReportProvisioning("Creating Microsoft Entra-protected endpoint...");
            var webSocketUri = await sandboxClient.ExposeProtectedPortAsync(
                sandboxId,
                MarkPortExposed,
                _lifetimeCts.Token);

            ReportProvisioning("Requesting endpoint access token...");
            var accessToken = await sandboxClient.GetProxyAccessTokenAsync(_lifetimeCts.Token);

            var terminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(120, 30)
                .WithRemoteTerminal(
                    webSocketUri,
                    webSocketOptions => webSocketOptions.SetRequestHeader(
                        "Authorization",
                        $"Bearer {accessToken}"))
                .WithTerminalWidget(out var handle)
                .Build();

            handle.StateChanged += _ => Invalidate();
            handle.WindowTitleChanged += _ => Invalidate();
            session = new ActiveSandboxSession(sandboxId, terminal, handle);

            lock (_sync)
            {
                _activeSession = session;
                _status = AzureSandboxDemoStatus.Running;
                _message = $"Connected to {webSocketUri.Host}. Exit the shell to destroy this sandbox.";
            }

            _app?.RequestFocus(node =>
                node is TerminalNode terminalNode && terminalNode.Handle == handle);
            Invalidate();

            await terminal.RunAsync(_lifetimeCts.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (AzureSandboxException exception)
        {
            failure = exception.Message;
        }
        catch (WebSocketException exception)
        {
            failure = $"Remote WebSocket connection failed: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            failure = $"Remote terminal failed: {exception.Message}";
        }
        finally
        {
            if (session is not null)
                await session.Terminal.DisposeAsync();

            lock (_sync)
                _activeSession = null;

            await CompleteCleanupAsync(failure);
        }
    }

    private async Task CompleteCleanupAsync(string? failure)
    {
        string? sandboxId;
        bool portWasExposed;
        lock (_sync)
        {
            sandboxId = _sandboxId;
            portWasExposed = _portWasExposed;
        }

        if (sandboxId is null)
        {
            if (!_lifetimeCts.IsCancellationRequested)
            {
                SetState(
                    AzureSandboxDemoStatus.Failed,
                    failure ?? "Sandbox provisioning did not complete.");
            }
            return;
        }

        SetState(AzureSandboxDemoStatus.CleaningUp, "Deleting the Azure sandbox...");

        try
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await sandboxClient.CleanupSandboxAsync(
                sandboxId,
                portWasExposed,
                ReportCleanup,
                cleanupCts.Token);

            lock (_sync)
            {
                _sandboxId = null;
                _portWasExposed = false;
            }

            if (!_lifetimeCts.IsCancellationRequested)
            {
                SetState(
                    failure is null
                        ? AzureSandboxDemoStatus.Ready
                        : AzureSandboxDemoStatus.Failed,
                    failure is null
                        ? "Sandbox deleted. You can create a new session."
                        : $"{failure} The sandbox was deleted.");
            }
        }
        catch (AzureSandboxException exception)
        {
            SetState(
                AzureSandboxDemoStatus.CleanupFailed,
                $"Cleanup failed: {exception.Message}");
        }
        catch (OperationCanceledException)
        {
            SetState(
                AzureSandboxDemoStatus.CleanupFailed,
                "Cleanup timed out. The sandbox may still exist.");
        }
    }

    private async Task RetryCleanupAsync()
    {
        await CompleteCleanupAsync(failure: null);
    }

    private void ReportProvisioning(string message)
        => SetState(AzureSandboxDemoStatus.Provisioning, message);

    private void ReportCleanup(string message)
        => SetState(AzureSandboxDemoStatus.CleaningUp, message);

    private void MarkPortExposed()
    {
        lock (_sync)
            _portWasExposed = true;
    }

    private static void WriteCleanupFailure(string sandboxId, string message)
    {
        Console.Error.WriteLine($"Unable to delete Azure sandbox '{sandboxId}': {message}");
        Console.Error.WriteLine(
            $"Delete it manually with: aca sandbox delete --id {sandboxId} --yes");
    }

    private void SetState(AzureSandboxDemoStatus status, string message)
    {
        lock (_sync)
        {
            _status = status;
            _message = message;
        }

        Invalidate();
    }

    private void Invalidate()
        => _app?.Invalidate();

    private static string StatusLabel(AzureSandboxDemoStatus status)
        => status switch
        {
            AzureSandboxDemoStatus.CleaningUp => "Cleaning up",
            AzureSandboxDemoStatus.CleanupFailed => "Cleanup failed",
            _ => status.ToString()
        };

    private static string ShortId(string sandboxId)
        => sandboxId.Length <= 8 ? sandboxId : sandboxId[..8];
}
