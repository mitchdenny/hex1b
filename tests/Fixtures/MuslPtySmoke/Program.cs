using Hex1b;
using Hex1b.Automation;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHeadless()
    .WithDimensions(80, 24)
    .WithPtyProcess("/bin/sh", "-c",
        "test -t 0 && test -t 1 || exit 10; " +
        "printf 'musl-pty-ready\\n'; IFS= read -r line; " +
        "test \"$line\" = 'hello-musl' || exit 11; " +
        "printf 'musl-pty-response:%s\\n' \"$line\"; IFS= read -r done; " +
        "test \"$done\" = 'done' || exit 12")
    .Build();
var run = terminal.RunAsync(timeout.Token);
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(snapshot => snapshot.ContainsText("musl-pty-ready"),
        TimeSpan.FromSeconds(10), "shell starts with a real PTY")
    .Type("hello-musl").Enter()
    .WaitUntil(snapshot => snapshot.ContainsText("musl-pty-response:hello-musl"),
        TimeSpan.FromSeconds(10), "shell reads input and returns output")
    .Type("done").Enter()
    .Build()
    .ApplyAsync(terminal, timeout.Token);
if (await run != 0)
    throw new InvalidOperationException("PTY shell exited unsuccessfully.");
Console.WriteLine("Musl PTY startup, input, output, and exit smoke test passed.");
