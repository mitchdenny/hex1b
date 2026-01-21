using Hex1b;

// AsciinemaPlaybackDemo - Demonstrates playing back asciinema (.cast) recordings

// Use the demo.cast file in the same directory
var castFile = Path.Combine(AppContext.BaseDirectory, "demo.cast");

if (!File.Exists(castFile))
{
    return 1;
}

// Create terminal with asciinema file playback
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaFile(castFile, speedMultiplier: 1.0)
    .Build();

// Run the terminal (plays back the recording)
return await terminal.RunAsync();
