using Hex1b;

// AsciinemaPlaybackDemo - Demonstrates playing back asciinema (.cast) recordings
Console.WriteLine("=== Asciinema Playback Demo ===\n");

// Use the demo.cast file in the same directory
var castFile = Path.Combine(AppContext.BaseDirectory, "demo.cast");

if (!File.Exists(castFile))
{
    Console.WriteLine($"Error: {castFile} not found!");
    return 1;
}

Console.WriteLine($"Playing back: {castFile}");
Console.WriteLine("Press Ctrl+C to exit during playback.\n");

// Create terminal with asciinema file playback
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaFile(castFile, speedMultiplier: 1.0)
    .Build();

Console.WriteLine("Starting playback...\n");

// Run the terminal (plays back the recording)
var exitCode = await terminal.RunAsync();

Console.WriteLine($"\n\nPlayback finished with exit code: {exitCode}");
return exitCode;
