using Hex1b.Terminal;
using Hex1b.Terminal.Automation;

if (!OperatingSystem.IsWindows())
{
    await Console.Error.WriteLineAsync("This demo requires Windows 10 22H2 or later with PTY support.");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

try
{
    // Launch pwsh.exe
    await using var process = new Hex1bTerminalChildProcess(
        "pwsh.exe",
        [],
        workingDirectory: Environment.CurrentDirectory,
        inheritEnvironment: true,
        initialWidth: width,
        initialHeight: height
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

    // Wait for process to exit
    try
    {
        await process.WaitForExitAsync(CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        process.Kill();
    }
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}

return 0;
