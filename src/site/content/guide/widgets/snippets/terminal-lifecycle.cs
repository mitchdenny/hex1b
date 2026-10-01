using Hex1b;

// Handle terminal lifecycle states
bashHandle.StateChanged += state =>
{
    switch (state)
    {
        case TerminalState.NotStarted:
            Console.WriteLine("Terminal not started yet");
            break;
        case TerminalState.Running:
            Console.WriteLine("Terminal is running");
            break;
        case TerminalState.Completed:
            Console.WriteLine($"Terminal exited with code {bashHandle.ExitCode}");
            break;
    }
};
