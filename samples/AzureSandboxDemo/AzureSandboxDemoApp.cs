using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace AzureSandboxDemo;

internal sealed class AzureSandboxDemoApp
{
    private const int InitialTerminalWidth = 100;
    private const int InitialTerminalHeight = 30;

    private readonly AcaCli _cli = new();
    private readonly AzureSandboxSettings _settings = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Hex1bApp? _app;
    private Hex1bTerminal? _sandboxTerminal;
    private TerminalWidgetHandle? _sandboxHandle;
    private CancellationTokenSource? _sandboxTerminalCts;
    private Task? _sandboxTerminalTask;
    private bool _busy;
    private string _status = "Checking Azure and ACA CLI prerequisites...";

    internal async Task RunAsync()
    {
        await InitializeDefaultsAsync();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithMouse()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    _app = app;
                    return context => Build(context);
                })
            .Build();

        try
        {
            await terminal.RunAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            await DisconnectTerminalAsync();
            _lifetime.Dispose();
        }
    }

    private Hex1bWidget Build(RootContext context)
        => context.HSplitter(
            setup => BuildSetupPane(setup),
            terminal => BuildTerminalPane(terminal),
            leftWidth: 48)
        .InputBindings(bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                .OverridesCapture()
                .Action(async _ => await DisconnectTerminalAsync(), "Disconnect shell");
            bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.Q)
                .OverridesCapture()
                .Action(_ => _lifetime.Cancel(), "Quit");
        });

    private Hex1bWidget[] BuildSetupPane<TParent>(WidgetContext<TParent> context)
        where TParent : Hex1bWidget
        =>
        [
            context.Text(" Azure Sandbox Demo"),
            context.Text(" Provision with aca; connect with the sample protocol adapter."),
            context.Separator(),
            Field(context, "Subscription", _settings.SubscriptionId, value => _settings.SubscriptionId = value),
            Field(context, "Resource group", _settings.ResourceGroup, value => _settings.ResourceGroup = value),
            Field(context, "Region", _settings.Region, value => _settings.Region = value),
            Field(context, "Sandbox group", _settings.SandboxGroup, value => _settings.SandboxGroup = value),
            Field(context, "Disk image", _settings.DiskImage, value => _settings.DiskImage = value),
            Field(context, "Shell command", _settings.Command, value => _settings.Command = value),
            Field(context, "Sandbox ID", _settings.SandboxId, value => _settings.SandboxId = value),
            context.Text(""),
            context.HStack(row =>
            [
                row.Button("Create group")
                    .OnClick(_ => StartOperation(CreateGroupAsync)),
                row.Text(" "),
                row.Button("Create sandbox + connect")
                    .OnClick(_ => StartOperation(CreateSandboxAndConnectAsync))
            ]).ContentHeight(),
            context.HStack(row =>
            [
                row.Button("Connect ID")
                    .OnClick(_ => StartOperation(ConnectExistingSandboxAsync)),
                row.Text(" "),
                row.Button("Disconnect")
                    .OnClick(async _ => await DisconnectTerminalAsync())
            ]).ContentHeight(),
            context.HStack(row =>
            [
                row.Button("Delete sandbox")
                    .OnClick(_ => StartOperation(DeleteSandboxAsync)),
                row.Text(" "),
                row.Button("Delete group")
                    .OnClick(_ => StartOperation(DeleteGroupAsync))
            ]).ContentHeight(),
            context.Text(""),
            context.Separator(),
            context.Text(_busy ? $" Working: {_status}" : $" Status: {_status}"),
            context.Text(" Resources remain in Azure until explicitly deleted.")
        ];

    private Hex1bWidget[] BuildTerminalPane<TParent>(WidgetContext<TParent> context)
        where TParent : Hex1bWidget
    {
        Hex1bWidget content = _sandboxHandle is null
            ? context.Center(
                context.VStack(message =>
                [
                    message.Text("No sandbox shell connected."),
                    message.Text(""),
                    message.Text("Create a sandbox or enter an existing sandbox ID.")
                ]))
            : context.Terminal(_sandboxHandle)
                .CopyModeBindings()
                .WhenNotRunning(args =>
                {
                    var detail = args.ExitCode is { } exitCode
                        ? $"Sandbox shell exited with code {exitCode}."
                        : "Sandbox shell disconnected.";
                    return context.Center(context.Text(detail));
                });

        return
        [
            context.Border(content.Fill())
                .Title(string.IsNullOrWhiteSpace(_settings.SandboxId)
                    ? "ACA sandbox shell"
                    : $"ACA sandbox {_settings.SandboxId}")
                .Fill(),
            context.InfoBar(
            [
                "Mouse", "Change pane",
                "Ctrl+B D", "Disconnect",
                "Ctrl+B Q", "Quit"
            ])
        ];
    }

    private static Hex1bWidget Field<TParent>(
        WidgetContext<TParent> context,
        string label,
        string value,
        Action<string> update)
        where TParent : Hex1bWidget
        => context.VStack(column =>
        [
            column.Text($" {label}:"),
            column.TextBox(value)
                .OnTextChanged(args => update(args.NewText))
                .FillWidth()
        ]).ContentHeight();

    private async Task InitializeDefaultsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.SubscriptionId))
            {
                _settings.SubscriptionId =
                    await _cli.GetActiveSubscriptionIdAsync(_lifetime.Token);
            }

            var version = await _cli.GetVersionAsync(_lifetime.Token);
            _status = $"Ready ({version}).";
        }
        catch (Exception ex)
        {
            _status = $"Prerequisite check failed: {OneLine(ex.Message)}";
        }
    }

    private void StartOperation(Func<CancellationToken, Task> operation)
    {
        if (_busy)
        {
            return;
        }

        _ = RunOperationAsync(operation);
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        _busy = true;
        Invalidate();
        try
        {
            await operation(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _status = $"Error: {OneLine(ex.Message)}";
        }
        finally
        {
            _busy = false;
            Invalidate();
        }
    }

    private async Task CreateGroupAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Snapshot();
        await _cli.CreateSandboxGroupAsync(settings, UpdateStatus, cancellationToken);
        _status = $"Sandbox group '{settings.SandboxGroup}' is ready.";
    }

    private async Task CreateSandboxAndConnectAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Snapshot();
        await _cli.WaitForDataPlaneAccessAsync(settings, UpdateStatus, cancellationToken);
        _status = $"Creating a '{settings.DiskImage}' sandbox...";
        Invalidate();

        _settings.SandboxId = await _cli.CreateSandboxAsync(settings, cancellationToken);
        _status = $"Sandbox {_settings.SandboxId} is running; opening PTY...";
        Invalidate();

        await ConnectAsync(_settings.SnapshotWithSandbox(), cancellationToken);
    }

    private Task ConnectExistingSandboxAsync(CancellationToken cancellationToken)
        => ConnectAsync(_settings.SnapshotWithSandbox(), cancellationToken);

    private async Task ConnectAsync(
        AzureSandboxSettings settings,
        CancellationToken cancellationToken)
    {
        await DisconnectTerminalAsync();
        await _cli.WaitForDataPlaneAccessAsync(settings, UpdateStatus, cancellationToken);
        _status = "Acquiring an ACA data-plane token...";
        Invalidate();

        var token = await _cli.GetDataPlaneTokenAsync(
            settings.SubscriptionId,
            cancellationToken);
        var adapter = new AzureSandboxWorkloadAdapter(settings, token);
        try
        {
            await adapter.ConnectAsync(
                InitialTerminalWidth,
                InitialTerminalHeight,
                cancellationToken);

            var terminalCts =
                CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(InitialTerminalWidth, InitialTerminalHeight)
                .WithWorkload(adapter)
                .WithScrollback()
                .WithTerminalWidget(out var handle)
                .Build();

            _sandboxTerminalCts = terminalCts;
            _sandboxTerminal = terminal;
            _sandboxHandle = handle;
            _sandboxTerminalTask =
                ObserveTerminalAsync(terminal, adapter, terminalCts.Token);
            _status = $"Connected to shell session {adapter.SessionId}.";
            _app?.RequestFocus(node => node is TerminalNode);
        }
        catch
        {
            await adapter.DisposeAsync();
            throw;
        }
    }

    private async Task ObserveTerminalAsync(
        Hex1bTerminal terminal,
        AzureSandboxWorkloadAdapter adapter,
        CancellationToken cancellationToken)
    {
        try
        {
            var exitCode = await terminal.RunAsync(cancellationToken);
            if (ReferenceEquals(_sandboxTerminal, terminal))
            {
                _status = adapter.ErrorMessage is { Length: > 0 } error
                    ? $"Shell failed: {OneLine(error)}"
                    : $"Shell exited with code {adapter.ExitCode ?? exitCode}.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_sandboxTerminal, terminal))
            {
                _status = $"Terminal failed: {OneLine(ex.Message)}";
            }
        }
        finally
        {
            Invalidate();
        }
    }

    private async Task DisconnectTerminalAsync()
    {
        var terminal = _sandboxTerminal;
        var terminalCts = _sandboxTerminalCts;
        var terminalTask = _sandboxTerminalTask;

        _sandboxTerminal = null;
        _sandboxHandle = null;
        _sandboxTerminalCts = null;
        _sandboxTerminalTask = null;

        if (terminalCts is not null)
        {
            await terminalCts.CancelAsync();
        }

        if (terminalTask is not null)
        {
            try
            {
                await terminalTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (terminal is not null)
        {
            await terminal.DisposeAsync();
            _status = "Sandbox shell disconnected.";
        }

        terminalCts?.Dispose();
        Invalidate();
    }

    private async Task DeleteSandboxAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.SnapshotWithSandbox();
        await DisconnectTerminalAsync();
        _status = $"Deleting sandbox {settings.SandboxId}...";
        Invalidate();
        await _cli.DeleteSandboxAsync(settings, cancellationToken);
        _settings.SandboxId = "";
        _status = $"Deleted sandbox {settings.SandboxId}.";
    }

    private async Task DeleteGroupAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Snapshot();
        await DisconnectTerminalAsync();
        _status = $"Deleting sandbox group '{settings.SandboxGroup}'...";
        Invalidate();
        await _cli.DeleteSandboxGroupAsync(settings, cancellationToken);
        _settings.SandboxId = "";
        _status = $"Deleted sandbox group '{settings.SandboxGroup}'.";
    }

    private void UpdateStatus(string status)
    {
        _status = status;
        Invalidate();
    }

    private void Invalidate() => _app?.Invalidate();

    private static string OneLine(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
