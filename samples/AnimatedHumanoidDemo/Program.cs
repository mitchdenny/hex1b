using AnimatedHumanoidDemo;
using Hex1b;

var demo = new HumanoidDemo(new AnimatedHumanoid(AnimatedHumanoid.AssetPath));
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => options.EnableMouse = true, _ => context => demo.Build(context))
    .Build();
await terminal.RunAsync();
