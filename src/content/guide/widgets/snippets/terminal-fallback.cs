using Hex1b;

var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var bashHandle)
    .Build();

_ = terminal.RunAsync();

// Display a fallback widget when the terminal exits
ctx.Terminal(bashHandle)
    .WhenNotRunning(args => ctx.VStack(v => [
        v.Text($"Terminal exited with code {args.ExitCode}"),
        v.Button("Restart").OnClick(_ => RestartTerminal())
    ]));
