using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace EncryptedMuxerDemo;

/// <summary>
/// TUI application that provides a session list and terminal view
/// with tmux-style keyboard chords for navigation.
/// All connections are TLS-encrypted.
/// </summary>
internal sealed class SessionManagerApp
{
    private readonly SessionManager _sessions;
    private Hex1bApp? _app;

    public SessionManagerApp(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public async Task RunAsync()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithMouse()
            .WithHex1bApp((app, _) =>
            {
                _app = app;
                return ctx =>
                {
                    Hex1bWidget content;
                    if (_sessions.IsConnected)
                        content = BuildTerminalView(ctx, _sessions.Handle!);
                    else
                        content = BuildSessionListView(ctx);

                    return content.WithInputBindings(bindings =>
                    {
                        bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                            .OverridesCapture()
                            .Action(_ => _app?.RequestStop(), "Detach");

                        bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.S)
                            .OverridesCapture()
                            .Action(async _ =>
                            {
                                await _sessions.DisconnectAsync();
                                _sessions.StatusMessage = null;
                                _app?.Invalidate();
                            }, "Sessions");

                        bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.X)
                            .OverridesCapture()
                            .Action(async _ => await _sessions.KillCurrentSessionAsync(
                                () => _app?.Invalidate()), "Kill session");
                    });
                };
            })
            .Build();

        await terminal.RunAsync();

        await _sessions.DisconnectAsync();
        _sessions.KillAllServerProcesses();
    }

    private Hex1bWidget BuildSessionListView<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        var sessions = _sessions.DiscoverSessions();
        var items = sessions.Select(s => s.Name).Append("+ New Session").ToList();

        return ctx.VStack(v =>
        [
            v.Text(" \U0001f512 Hex1b Encrypted Muxer Demo "),
            v.Text(""),
            v.Text(_sessions.StatusMessage ?? "Select a session or create a new one:"),
            v.Text("  All connections are TLS-encrypted."),
            v.Text(""),
            v.List(items)
                .OnItemActivated(e =>
                {
                    if (e.ActivatedIndex == sessions.Count)
                        _ = _sessions.CreateAndConnectSessionAsync(() => _app?.Invalidate());
                    else
                        _ = _sessions.ConnectToSessionAsync(sessions[e.ActivatedIndex],
                            () => _app?.Invalidate());
                })
                .FillHeight(),
            v.InfoBar(s =>
            [
                s.Section("Enter"),
                s.Section("Select"),
                s.Spacer(),
                s.Section("Ctrl+B D"),
                s.Section("Quit")
            ]).WithDefaultSeparator(" ")
        ]);
    }

    private Hex1bWidget BuildTerminalView<TParent>(WidgetContext<TParent> ctx, TerminalWidgetHandle handle)
        where TParent : Hex1bWidget
    {
        var dims = _sessions.Adapter is not null
            ? $"{_sessions.Adapter.RemoteWidth}\u00d7{_sessions.Adapter.RemoteHeight}"
            : "";

        return ctx.VStack(v =>
        [
            v.Terminal(handle).CopyModeBindings().Fill(),
            v.InfoBar(s =>
            [
                s.Section("Ctrl+B S"),
                s.Section("Sessions"),
                s.Spacer(),
                s.Section("Ctrl+B X"),
                s.Section("Kill"),
                s.Spacer(),
                s.Section("Ctrl+B D"),
                s.Section("Detach"),
                s.Spacer(),
                s.Section("\U0001f512 TLS"),
                s.Section(_sessions.ConnectedSessionName ?? ""),
                s.Section(dims)
            ]).WithDefaultSeparator(" ")
        ]);
    }
}
