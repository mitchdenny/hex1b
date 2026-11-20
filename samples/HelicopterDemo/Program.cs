using HelicopterDemo;
using Hex1b;

var demo = new HelicopterDemoApp();
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(context => demo.Build(context))
    .WithMouse()
    .Build();
await terminal.RunAsync();
