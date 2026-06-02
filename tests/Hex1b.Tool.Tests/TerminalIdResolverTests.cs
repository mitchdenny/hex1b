using Hex1b.Tool.Infrastructure;

namespace Hex1b.Tool.Tests;

[TestClass]
public class TerminalIdResolverTests
{
    [TestMethod]
    public void Resolve_NoTerminals_ReturnsError()
    {
        var resolver = CreateResolver([]);

        var result = resolver.Resolve("123");

        Assert.IsFalse(result.Success);
        Assert.Contains("No terminals found", result.Error);
    }

    [TestMethod]
    public void Resolve_ExactMatch_ReturnsTerminal()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("12345");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("12345", result.Id);
        Assert.IsNotNull(result.SocketPath);
        Assert.EndsWith(".diagnostics.socket", result.SocketPath);
        Assert.AreEqual("tui", result.Type);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void Resolve_PrefixMatch_ReturnsTerminal()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui"),
            new TerminalDiscovery.DiscoveredTerminal("67890", "/tmp/67890.diagnostics.socket", "host")
        ]);

        var result = resolver.Resolve("123");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("12345", result.Id);
    }

    [TestMethod]
    public void Resolve_AmbiguousPrefix_ReturnsError()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui"),
            new TerminalDiscovery.DiscoveredTerminal("12399", "/tmp/12399.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("123");

        Assert.IsFalse(result.Success);
        Assert.Contains("Ambiguous", result.Error);
        Assert.Contains("12345", result.Error);
        Assert.Contains("12399", result.Error);
    }

    [TestMethod]
    public void Resolve_NoMatch_ReturnsError()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("12345", "/tmp/12345.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("999");

        Assert.IsFalse(result.Success);
        Assert.Contains("No terminal found matching '999'", result.Error);
    }

    [TestMethod]
    public void Resolve_CaseInsensitive_MatchesPrefix()
    {
        var resolver = CreateResolver([
            new TerminalDiscovery.DiscoveredTerminal("ABC123", "/tmp/ABC123.diagnostics.socket", "tui")
        ]);

        var result = resolver.Resolve("abc");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("ABC123", result.Id);
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
