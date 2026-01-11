using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;

// Counter for recording files
var recordingCounter = 0;

// Subprocess options to show in the menu
var options = new List<SubProcessOption>
{
    new("Bash Shell (PTY)", SubProcessType.PtyProcess, "/bin/bash", ["--norc"]),
    new("Simple Counter - C# (Process)", SubProcessType.Process, "dotnet", ["run", "--file", "SimpleCounter.cs"]),
    new("Simple Counter - C# (PTY Process)", SubProcessType.PtyProcess, "dotnet", ["run", "--file", "SimpleCounter.cs"]),
    new("Simple Counter - Python (Process)", SubProcessType.Process, "python3", ["simple_counter.py"]),
    new("Simple Counter - Python (PTY Process)", SubProcessType.PtyProcess, "python3", ["simple_counter.py"]),
    new("Silent Waiter - C# (Process)", SubProcessType.Process, "dotnet", ["run", "--file", "SilentWaiter.cs"]),
    new("Silent Waiter - C# (PTY Process)", SubProcessType.PtyProcess, "dotnet", ["run", "--file", "SilentWaiter.cs"]),
    new("dotnet --info (Process)", SubProcessType.Process, "dotnet", ["--info"]),
    new("dotnet --info (PTY Process)", SubProcessType.PtyProcess, "dotnet", ["--info"]),
    new("Exit", SubProcessType.Exit, ""),
};

while (true)
{
    // Step 1: Show a Hex1bApp with a centered list to select what to launch
    SubProcessOption? selectedOption = null;
    
    await Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, appOptions) =>
        {
            return ctx => ctx
                .ZStack(z =>
                [
                    // Background
                    z.Text(" "),
                    
                    // Centered menu
                    z.Align(Alignment.Center,
                        z.Border(
                            z.VStack(v =>
                            [
                                v.Text("Select a subprocess to launch:"),
                                v.Text(""),
                                v.List(options.Select(o => o.Label).ToArray())
                                    .OnItemActivated(args =>
                                    {
                                        selectedOption = options[args.ActivatedIndex];
                                        app.RequestStop();
                                    }),
                            ]),
                            title: "SubProcess Demo"
                        )
                    )
                ]);
        })
        .RunAsync();
    
    // Step 2: Handle the selection
    if (selectedOption == null || selectedOption.Type == SubProcessType.Exit)
    {
        Console.WriteLine("Goodbye!");
        break;
    }
    
    // Step 3: Launch the selected subprocess with Asciinema recording
    recordingCounter++;
    // Use absolute path in SubProcessDemo directory
    var recordingDir = Path.GetDirectoryName(typeof(SubProcessOption).Assembly.Location) ?? ".";
    var recordingFile = Path.Combine(recordingDir, $"recording_{recordingCounter:D3}_{SanitizeForFilename(selectedOption.Label)}.cast");
    
    Console.WriteLine($"Launching: {selectedOption.Label}...");
    Console.WriteLine($"Recording to: {recordingFile}");
    
    var builder = Hex1bTerminal.CreateBuilder()
        .WithAsciinemaRecording(recordingFile);
    
    switch (selectedOption.Type)
    {
        case SubProcessType.PtyProcess:
            builder.WithPtyProcess(selectedOption.FileName, selectedOption.Arguments);
            break;
            
        case SubProcessType.Process:
            builder.WithProcess(selectedOption.FileName, selectedOption.Arguments);
            break;
    }
    
    await builder.RunAsync();
    
    // Loop back to menu after subprocess exits
    Console.WriteLine($"\nSubprocess exited. Recording saved to: {recordingFile}");
    Console.WriteLine("Press any key to return to menu...");
    Console.ReadKey(intercept: true);
}

// Helper to sanitize labels for filenames
static string SanitizeForFilename(string label)
{
    return string.Concat(label
        .Replace(' ', '_')
        .Replace('-', '_')
        .Where(c => char.IsLetterOrDigit(c) || c == '_'));
}

// === Types ===

enum SubProcessType
{
    PtyProcess,
    Process,
    Exit
}

record SubProcessOption(
    string Label,
    SubProcessType Type,
    string FileName,
    string[] Arguments = null!)
{
    public string[] Arguments { get; init; } = Arguments ?? [];
}
