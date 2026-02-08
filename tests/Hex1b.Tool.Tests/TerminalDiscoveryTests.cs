using Hex1b.Tool.Infrastructure;

namespace Hex1b.Tool.Tests;

public class TerminalDiscoveryTests
{
    [Fact]
    public void Scan_DiagnosticsSocket_ParsesAsTui()
    {
        using var dir = new TempSocketDirectory();
        dir.CreateFile("12345.diagnostics.socket");

        var discovery = new TerminalDiscovery(dir.Path);
        var terminals = discovery.Scan();

        Assert.Single(terminals);
        Assert.Equal("12345", terminals[0].Id);
        Assert.Equal("tui", terminals[0].Type);
        Assert.EndsWith(".diagnostics.socket", terminals[0].SocketPath);
    }

    [Fact]
    public void Scan_TerminalSocket_ParsesAsHost()
    {
        using var dir = new TempSocketDirectory();
        dir.CreateFile("67890.terminal.socket");

        var discovery = new TerminalDiscovery(dir.Path);
        var terminals = discovery.Scan();

        Assert.Single(terminals);
        Assert.Equal("67890", terminals[0].Id);
        Assert.Equal("host", terminals[0].Type);
    }

    [Fact]
    public void Scan_MixedSocketTypes_ReturnsBoth()
    {
        using var dir = new TempSocketDirectory();
        dir.CreateFile("111.diagnostics.socket");
        dir.CreateFile("222.terminal.socket");

        var discovery = new TerminalDiscovery(dir.Path);
        var terminals = discovery.Scan();

        Assert.Equal(2, terminals.Count);
        Assert.Contains(terminals, t => t.Id == "111" && t.Type == "tui");
        Assert.Contains(terminals, t => t.Id == "222" && t.Type == "host");
    }

    [Fact]
    public void Scan_UnknownSocketPattern_Ignored()
    {
        using var dir = new TempSocketDirectory();
        dir.CreateFile("12345.diagnostics.socket");
        dir.CreateFile("random.socket");
        dir.CreateFile("not-a-socket.txt");

        var discovery = new TerminalDiscovery(dir.Path);
        var terminals = discovery.Scan();

        Assert.Single(terminals);
        Assert.Equal("12345", terminals[0].Id);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsEmpty()
    {
        using var dir = new TempSocketDirectory();
        var discovery = new TerminalDiscovery(dir.Path);

        var terminals = discovery.Scan();

        Assert.Empty(terminals);
    }

    [Fact]
    public void Scan_NonexistentDirectory_ReturnsEmpty()
    {
        var discovery = new TerminalDiscovery("/tmp/nonexistent-hex1b-test-" + Guid.NewGuid());
        var terminals = discovery.Scan();

        Assert.Empty(terminals);
    }

    private sealed class TempSocketDirectory : IDisposable
    {
        public string Path { get; }

        public TempSocketDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hex1b-test-" + Guid.NewGuid());
            Directory.CreateDirectory(Path);
        }

        public void CreateFile(string name)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, name), "");
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
