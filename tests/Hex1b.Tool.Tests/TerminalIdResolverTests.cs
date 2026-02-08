using Hex1b.Tool.Infrastructure;

namespace Hex1b.Tool.Tests;

public class TerminalIdResolverTests
{
    [Fact]
    public void Resolve_NoTerminals_ReturnsError()
    {
        var resolver = CreateResolver([]);

        var result = resolver.Resolve("123");

        Assert.False(result.Success);
        Assert.Contains("No terminals found", result.Error);
    }

    [Fact]
    public void Resolve_ExactMatch_ReturnsTerminal()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("12345");

        Assert.True(result.Success);
        Assert.Equal("12345", result.Id);
        Assert.NotNull(result.SocketPath);
        Assert.EndsWith(".diagnostics.socket", result.SocketPath);
        Assert.Equal("tui", result.Type);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Resolve_PrefixMatch_ReturnsTerminal()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui"),
            new TerminalDiscovery.DiscoveredTerminal("67890", "/tmp/67890.diagnostics.socket", "host")
        ]);

        var result = resolver.Resolve("123");

        Assert.True(result.Success);
        Assert.Equal("12345", result.Id);
    }

    [Fact]
    public void Resolve_AmbiguousPrefix_ReturnsError()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui"),
            new TerminalDiscovery.DiscoveredTerminal("12399", "/tmp/12399.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("123");

        Assert.False(result.Success);
        Assert.Contains("Ambiguous", result.Error);
        Assert.Contains("12345", result.Error);
        Assert.Contains("12399", result.Error);
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsError()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("999");

        Assert.False(result.Success);
        Assert.Contains("No terminal found matching '999'", result.Error);
    }

    [Fact]
    public void Resolve_CaseInsensitive_MatchesPrefix()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("ABC123", "/tmp/ABC123.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("abc");

        Assert.True(result.Success);
        Assert.Equal("ABC123", result.Id);
    }

    /// <summary>
    /// Creates a resolver backed by a temp directory with the given terminals as socket files.
    /// </summary>
    private static TerminalIdResolver CreateResolver(TerminalDiscovery.DiscoveredTerminal[] terminals)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hex1b-resolver-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);

        foreach (var t in terminals)
        {
            var suffix = t.Type == "host" ? "terminal" : "diagnostics";
            File.WriteAllText(Path.Combine(dir, $"{t.Id}.{suffix}.socket"), "");
        }

        var discovery = new TerminalDiscovery(dir);
        return new TerminalIdResolver(discovery);
    }
}
