using Hex1b.Terminal;
using Hex1b.Terminal.Automation;

if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
{
    await Console.Error.WriteLineAsync("This demo requires Linux or macOS.");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

// Asciinema recording file - delete if exists
var castFile = Path.Combine(Environment.CurrentDirectory, "tmux-demo.cast");
if (File.Exists(castFile))
{
    File.Delete(castFile);
}

try
{
    // Launch bash
    await using var process = new Hex1bTerminalChildProcess(
        "/bin/bash",
        ["--norc", "--noprofile"],
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
    
    // Add asciinema recorder
    var recorder = terminalOptions.AddAsciinemaRecorder(castFile, new AsciinemaRecorderOptions
    {
        Title = "Tmux Demo",
        CaptureInput = true
    });
    
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
    
    await recorder.FlushAsync();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}

return 0;
