using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.Tape;

public partial class TapeConformanceTests
{
    [TestMethod]
    [DynamicData(nameof(SourceCases))]
    public async Task ResolveAsync_SourceBundles_MatchesExpandedOracleCommands(string id)
    {
        var fixture = Fixtures.Cases.Single(item => item.Id == id);
        var directory = Path.GetFullPath(Path.Combine("TestResults", "TapeConformance", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (name, text) in fixture.Files)
            {
                var path = Path.Combine(directory, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);
            }
            var parser = new TapeParser();
            var document = parser.Parse(GetText(fixture), id);
            var diagnostics = new List<TapeDiagnostic>();
            var commands = await TapeSourceResolver.ResolveAsync(document, parser, directory,
                diagnostics, TestContext.Current.CancellationToken);
            if (fixture.Status == "success")
            {
                Assert.IsEmpty(diagnostics, id);
                var expanded = commands.Where(command => command is not TapeSourceCommand && !command.IsIncludedOutput);
                TestSeq.AreEqual(fixture.Commands, expanded.Select(Normalize), id);
                foreach (var command in commands.Where(command => command.Span.SourceName != id))
                {
                    Assert.IsNotNull(command.Span.SourceName);
                    Assert.IsTrue(command.Span.SourceName.StartsWith(directory, StringComparison.Ordinal));
                    Assert.IsTrue(File.Exists(command.Span.SourceName));
                }
            }
            else
            {
                Assert.IsNotEmpty(diagnostics, $"{id}: upstream {fixture.Status} must fail resolution.");
                Assert.IsTrue(diagnostics.Any(d => d.Severity == TapeDiagnosticSeverity.Error));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
