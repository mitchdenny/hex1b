namespace WebTerminalDemo;

internal sealed record CreateTerminalRequest(string Scene = "mixed", int Columns = 100, int Rows = 30, string? Name = null);
