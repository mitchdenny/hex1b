
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

AsciinemaRecorder? recorder = null;

try
{
    await Hex1bTerminal.CreateBuilder()
        .WithPtyProcess("/bin/bash", "--norc", "--noprofile")
        .WithDimensions(width, height)
        .WithAsciinemaRecording(castFile, r => recorder = r, new AsciinemaRecorderOptions
        {
            Title = "Tmux Demo",
            CaptureInput = true
        })
        .RunAsync();
    
    if (recorder != null)
    {
        await recorder.FlushAsync();
    }
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}

return 0;
