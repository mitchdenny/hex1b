using Hex1b;
using OSC7501Demo;

var shells = ShellDiscovery.Detect();
var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
await using var demo = new Osc7501DemoApp(shells, terminalWidth: Math.Max(20, width * 2 / 3));
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(_ => { }, app => context => demo.Build(context, app))
    .Build();

await terminal.RunAsync();
