using Hex1b.Terminal;

if (!OperatingSystem.IsWindows())
{
    await Console.Error.WriteLineAsync("This demo requires Windows 10 22H2 or later with PTY support.");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

try
{
    await Hex1bTerminal.CreateBuilder()
        .WithPtyProcess("pwsh.exe")
        .WithDimensions(width, height)
        .RunAsync();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}

return 0;
