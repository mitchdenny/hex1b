using Hex1b.Terminal;

if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
{
    Console.WriteLine("This demo requires Linux or macOS.");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

try
{
    await using var process = new Hex1bTerminalChildProcess(
        "/bin/bash",
        [],
        workingDirectory: Environment.CurrentDirectory,
        inheritEnvironment: true,
        initialWidth: width,
        initialHeight: height,
        environment: new Dictionary<string, string> { ["PS1"] = "tmuxdemo $ " }
    );
    
    var presentation = new ConsolePresentationAdapter(enableMouse: false);
    
    var terminalOptions = new Hex1bTerminalOptions
    {
        Width = width,
        Height = height,
        PresentationAdapter = presentation,
        WorkloadAdapter = process
    };
    
    using var terminal = new Hex1bTerminal(terminalOptions);
    await process.StartAsync();
    
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    
    try
    {
        await process.WaitForExitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        process.Kill();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

return 0;

