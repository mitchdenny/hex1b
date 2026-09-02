using AzureSandboxDemo;
using Hex1b;

var options = AzureSandboxDemoOptions.Parse(args, out var error);
if (options is null)
{
    if (error is not null)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
    }

    Console.WriteLine(AzureSandboxDemoOptions.Usage);
    return error is null ? 0 : 1;
}

var controller = new AzureSandboxDemoController(
    options,
    new AzureSandboxClient(options, new CommandRunner()));

await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(
        _ => { },
        app =>
        {
            controller.Attach(app);
            return controller.Build;
        })
    .Build();

try
{
    return await displayTerminal.RunAsync();
}
finally
{
    await controller.DisposeAsync();
}
