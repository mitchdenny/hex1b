using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.Tape;

[TestClass]
public partial class TapeConformanceTests
{
    private const string Reference = "c073383b5de0b1f57bf514113029c306bc986539";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Corpus Fixtures = JsonSerializer.Deserialize<Corpus>(ReadResource("expectations.json"), JsonOptions)!;

    public static IEnumerable<object[]> Cases() => Fixtures.Cases.Select(fixture => new object[] { fixture.Id });

    public static IEnumerable<object[]> SourceCases() => Fixtures.Cases
        .Where(fixture => fixture.Syntax?.Status == "success").Select(fixture => new object[] { fixture.Id });

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void Parse_OracleCorpus_MatchesNormalizedCommandStreamAndTryParse(string id)
    {
        var fixture = Fixtures.Cases.Single(item => item.Id == id);
        var text = GetText(fixture);
        var expected = fixture.Syntax ?? fixture;
        var parser = new TapeParser();
        Assert.AreEqual(fixture.InputSha256,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))), id);

        var success = parser.TryParse(text, out var document, out var diagnostics, id);
        Assert.AreEqual(expected.Status == "success", success,
            $"{id}: expected {expected.Status}; {string.Join("; ", diagnostics.Select(d => d.Message))}");
        Assert.AreEqual(success, parser.TryParse(text, out var simpleDocument, id), id);
        TestSeq.AreEqual(document?.Commands.Select(Normalize) ?? [], simpleDocument?.Commands.Select(Normalize) ?? []);

        if (success)
        {
            Assert.IsNotNull(document);
            Assert.IsEmpty(diagnostics, id);
            Assert.AreEqual(id, document.SourceName);
            var parsed = parser.Parse(text, id);
            TestSeq.AreEqual(expected.Commands, document.Commands.Select(Normalize), id);
            TestSeq.AreEqual(document.Commands.Select(Normalize), parsed.Commands.Select(Normalize), id);
            foreach (var command in document.Commands)
            {
                Assert.AreEqual(id, command.Span.SourceName);
                Assert.IsTrue(command.Span.Offset >= 0 && command.Span.Offset <= text.Length);
                Assert.IsTrue(command.Span.Length >= 0 && command.Span.Offset + command.Span.Length <= text.Length);
            }
        }
        else
        {
            Assert.IsNull(document, id);
            Assert.IsNull(simpleDocument, id);
            Assert.IsNotEmpty(diagnostics, id);
            var error = Assert.ThrowsExactly<TapeParseException>(() => parser.Parse(text, id));
            TestSeq.AreEqual(diagnostics, error.Diagnostics, id);
            foreach (var diagnostic in diagnostics)
            {
                Assert.AreEqual(TapeDiagnosticStage.Parsing, diagnostic.Stage);
                Assert.AreEqual(TapeDiagnosticSeverity.Error, diagnostic.Severity);
                Assert.AreEqual(id, diagnostic.Span.SourceName);
                Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostic.Code));
                Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostic.Message));
            }
            // Go diagnoses each illegal UTF-8 byte; managed source spans identify
            // the offending Unicode character instead. Acceptance still agrees.
            if (id is not ("supplemental/unicode-bare" or "supplemental/bom"))
            {
                TestSeq.AreEqual(expected.Errors.Select(e => (e.Token.Line, e.Token.Column)),
                    diagnostics.Select(d => (d.Span.Line, d.Span.Column)), id);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void Lexer_OracleCorpus_MatchesNormalizedTokens(string id)
    {
        var fixture = Fixtures.Cases.Single(item => item.Id == id);
        var lexer = new TapeLexer(GetText(fixture), id);
        var tokens = new List<OracleToken>();
        for (var i = 0; i <= GetText(fixture).Length * 2 + 10; i++)
        {
            var token = lexer.Next();
            tokens.Add(new OracleToken
            {
                Type = NormalizeKind(token),
                Literal = token.Value,
                Line = token.Span.Line,
                Column = token.Span.Column
            });
            if (token.Kind == TapeTokenKind.EndOfFile)
                break;
        }
        Assert.AreEqual("EOF", tokens[^1].Type, id);
        if (id is "supplemental/unicode-bare" or "supplemental/bom")
        {
            // Byte-wise illegal literals are not meaningful Unicode strings.
            // Compare the precise byte stream represented by each token instead.
            var projected = tokens.SelectMany(t => t.Type == "ILLEGAL"
                ? Encoding.UTF8.GetBytes(t.Literal).Select((value, index) => t with
                {
                    Literal = ((char)value).ToString(),
                    Column = t.Column + index
                })
                : [t]);
            TestSeq.AreEqual(fixture.Tokens, projected, id);
        }
        else
        {
            TestSeq.AreEqual(fixture.Tokens, tokens, id);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public async Task ParseAsync_OracleCorpus_MatchesStringParserForReaderStreamAndFile(string id)
    {
        var fixture = Fixtures.Cases.Single(item => item.Id == id);
        var text = GetText(fixture);
        var parser = new TapeParser();
        var directory = Path.GetFullPath(Path.Combine("TestResults", "TapeConformance", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            var file = new FileInfo(Path.Combine(directory, "input.tape"));
            await File.WriteAllBytesAsync(file.FullName, Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);
            using var reader = new StringReader(text);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            if (parser.TryParse(text, out var document, out var diagnostics, file.FullName))
            {
                var fromReader = await parser.ParseAsync(reader, file.FullName, TestContext.Current.CancellationToken);
                var fromStream = await parser.ParseAsync(stream, file.FullName, TestContext.Current.CancellationToken);
                var fromFile = await parser.ParseAsync(file, TestContext.Current.CancellationToken);
                TestSeq.AreEqual(document.Commands.Select(Normalize), fromReader.Commands.Select(Normalize), id);
                TestSeq.AreEqual(document.Commands.Select(Normalize), fromStream.Commands.Select(Normalize), id);
                TestSeq.AreEqual(document.Commands.Select(Normalize), fromFile.Commands.Select(Normalize), id);
                TestSeq.AreEqual(document.Commands.Select(c => c.Span), fromFile.Commands.Select(c => c.Span), id);
            }
            else
            {
                var fromReader = await Assert.ThrowsExactlyAsync<TapeParseException>(
                    () => parser.ParseAsync(reader, file.FullName, TestContext.Current.CancellationToken));
                var fromStream = await Assert.ThrowsExactlyAsync<TapeParseException>(
                    () => parser.ParseAsync(stream, file.FullName, TestContext.Current.CancellationToken));
                var fromFile = await Assert.ThrowsExactlyAsync<TapeParseException>(
                    () => parser.ParseAsync(file, TestContext.Current.CancellationToken));
                TestSeq.AreEqual(diagnostics, fromReader.Diagnostics, id);
                TestSeq.AreEqual(diagnostics, fromStream.Diagnostics, id);
                TestSeq.AreEqual(diagnostics, fromFile.Diagnostics, id);
            }
            Assert.IsTrue(stream.CanRead, "Stream ownership remains with the caller.");
            Assert.AreEqual(-1, reader.Read(), "Reader ownership remains with the caller.");
            using var exclusive = File.Open(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Corpus_PinnedInventory_PreservesEveryTapeAndLicense()
    {
        var provenance = JsonSerializer.Deserialize<Provenance>(ReadResource("provenance.json"), JsonOptions)!;
        Assert.AreEqual(Reference, provenance.Ref);
        Assert.AreEqual(Reference, Fixtures.Ref);
        Assert.AreEqual("go1.25.12", Fixtures.GoVersion);
        var tapes = provenance.Files.Where(file => file.Path.EndsWith(".tape", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(106, tapes);
        Assert.AreEqual(27891, tapes.Sum(file => file.Bytes));
        foreach (var file in provenance.Files.Where(file => file.Path == "LICENSE" || file.Path.EndsWith(".tape", StringComparison.Ordinal)))
        {
            var bytes = ReadBytes("upstream/" + file.Path);
            Assert.AreEqual(file.Bytes, bytes.Length, file.Path);
            Assert.AreEqual(file.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)), file.Path);
            Assert.AreEqual(Reference, file.Ref);
            if (file.Path != "LICENSE")
                Assert.HasCount(1, Fixtures.Cases.Where(item => item.Path == "upstream/" + file.Path).ToArray(), file.Path);
        }
        StringAssert.Contains(ReadResource("upstream/LICENSE"), "MIT License");
        Assert.AreEqual("error", Fixtures.Cases.Single(item => item.Id == "upstream/examples/errors/parser.tape").Status);
    }

    [TestMethod]
    public void Corpus_CommandAndSettingInventory_HasSuccessfulTypedAstCoverage()
    {
        var documents = Fixtures.Cases.Where(f => (f.Syntax ?? f).Status == "success")
            .Select(f => new TapeParser().Parse(GetText(f), f.Id)).ToArray();
        var actualCommands = documents.SelectMany(document => document.Commands).Select(Normalize)
            .Select(command => command.Type).Distinct().Order().ToArray();
        TestSeq.AreEqual(Fixtures.CommandInventory, actualCommands);
        var actualSettings = documents.SelectMany(document => document.Commands).OfType<TapeSetCommand>()
            .Select(command => command.Setting.ToString()).Distinct().Order().ToArray();
        TestSeq.AreEqual(Fixtures.SettingInventory, actualSettings);
        TestSeq.AreEqual(Fixtures.SettingInventory, Enum.GetNames<TapeSetting>().Order());
        var invalidCommands = Fixtures.Cases.Where(f => f.Id.StartsWith("supplemental/command-invalid/", StringComparison.Ordinal))
            .Select(f =>
            {
                Assert.AreEqual("error", f.Status, f.Id);
                return KeywordType(f.Id.Split('/')[^1]);
            }).Order();
        TestSeq.AreEqual(Fixtures.CommandInventory, invalidCommands);
        var invalidSettings = Fixtures.Cases.Where(f => f.Id.StartsWith("supplemental/setting-invalid/", StringComparison.Ordinal))
            .Select(f =>
            {
                Assert.AreEqual("error", f.Status, f.Id);
                return f.Id.Split('/')[^1];
            }).Order();
        TestSeq.AreEqual(Fixtures.SettingInventory, invalidSettings);
    }

    [TestMethod]
    [DataRow("gif")]
    [DataRow("mp4")]
    [DataRow("webm")]
    [DataRow("arbitrary")]
    public void Parse_UnsupportedVideoAndPresentationSyntax_ReturnsTypedAst(string extension)
    {
        var text = $"Output demo.{extension}\nSet Width 1200\nSet FontFamily 'Missing Font'\nScreenshot frame.png\nRequire missing-tool";
        var commands = new TapeParser().Parse(text, "unsupported.tape").Commands;
        Assert.AreEqual($"demo.{extension}", TestSeq.IsType<TapeOutputCommand>(commands[0]).Path);
        Assert.AreEqual("1200", TestSeq.IsType<TapeSetCommand>(commands[1]).Value.Value);
        Assert.AreEqual("Missing Font", TestSeq.IsType<TapeSetCommand>(commands[2]).Value.Value);
        Assert.AreEqual("frame.png", TestSeq.IsType<TapeScreenshotCommand>(commands[3]).Path);
        Assert.AreEqual("missing-tool", TestSeq.IsType<TapeRequireCommand>(commands[4]).Program);
    }

    [TestMethod]
    [DataRow("supplemental/source-cycle")]
    [DataRow("supplemental/source-self-cycle")]
    public void Parse_CyclicSourceFixtures_RetainsSourceWithoutOpeningFiles(string id)
    {
        var fixture = Fixtures.Cases.Single(f => f.Id == id);
        Assert.AreEqual("guarded-source-cycle", fixture.Status);
        var source = TestSeq.IsType<TapeSourceCommand>(TestSeq.Single(new TapeParser().Parse(GetText(fixture), id).Commands));
        Assert.AreEqual("a.tape", source.Path);
        Assert.IsNotNull(fixture.Syntax);
        TestSeq.AreEqual(fixture.Syntax.Commands, new[] { Normalize(source) });
    }

    private static OracleCommand Normalize(TapeCommand command) => command switch
    {
        TapeOutputCommand output => new("OUTPUT", OutputExtension(output.Path), output.Path, ""),
        TapeSourceCommand source => new("SOURCE", "", source.Path, ""),
        TapeTypeCommand type => new("TYPE", type.Delay?.Value ?? "", type.Text, ""),
        TapeKeyCommand key => new(KeywordType(key.Key), key.Delay?.Value ?? "", key.Count?.Value ?? "1", ""),
        TapeChordCommand chord => new(chord.Modifier.ToUpperInvariant(), "", string.Join(" ", chord.Parts), ""),
        TapeSleepCommand sleep => new("SLEEP", "", sleep.Duration.Value, ""),
        TapeSetCommand setting => new("SET", setting.Setting.ToString(), setting.Value.Value, ""),
        TapeWaitCommand wait => new("WAIT", wait.Timeout?.Value ?? "",
            wait.Scope + (wait.Pattern is null ? "" : " " + wait.Pattern), ""),
        TapeEnvCommand environment => new("ENV", environment.Name, environment.Value, ""),
        TapeRequireCommand require => new("REQUIRE", "", require.Program, ""),
        TapeScreenshotCommand screenshot => new("SCREENSHOT", "", screenshot.Path, ""),
        TapeCopyCommand copy => new("COPY", "", copy.Text, ""),
        TapePasteCommand => new("PASTE", "", "", ""),
        TapeHideCommand => new("HIDE", "", "", ""),
        TapeShowCommand => new("SHOW", "", "", ""),
        _ => throw new AssertFailedException($"Unmapped AST command {command.GetType().Name}")
    };

    private static string OutputExtension(string path)
    {
        // VHS filepath.Ext on Unix treats backslashes as ordinary characters.
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash ? path[dot..] : ".png";
    }

    private static string NormalizeKind(TapeToken token) => token.Kind switch
    {
        TapeTokenKind.EndOfFile => "EOF",
        TapeTokenKind.Keyword or TapeTokenKind.Setting => KeywordType(token.Value),
        TapeTokenKind.Unit => token.Value switch
        {
            "ms" => "MILLISECONDS",
            "s" => "SECONDS",
            "m" => "MINUTES",
            _ => token.Value.ToUpperInvariant()
        },
        TapeTokenKind.At => "@",
        TapeTokenKind.Plus => "+",
        TapeTokenKind.Minus => "-",
        TapeTokenKind.Equal => "=",
        TapeTokenKind.Percent => "%",
        TapeTokenKind.LeftBracket => "[",
        TapeTokenKind.RightBracket => "]",
        TapeTokenKind.Caret => "^",
        TapeTokenKind.Backslash => "\\",
        _ => token.Kind.ToString().ToUpperInvariant()
    };

    private static string KeywordType(string keyword) => keyword == "BorderRadius"
        ? "CORNER_RADIUS"
        : Regex.Replace(keyword, "(?<!^)([A-Z])", "_$1").ToUpperInvariant();

    private static string GetText(Case fixture) => fixture.Path is { Length: > 0 }
        ? ReadResource(fixture.Path)
        : fixture.Text;

    private static string ReadResource(string path) => Encoding.UTF8.GetString(ReadBytes(path));

    private static byte[] ReadBytes(string path)
    {
        var assembly = typeof(TapeConformanceTests).Assembly;
        var name = assembly.GetManifestResourceNames().Single(name => name.Replace('\\', '/') == "TapeConformance/" + path);
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed class Corpus
    {
        public string Ref { get; init; } = "";
        public string GoVersion { get; init; } = "";
        public string[] CommandInventory { get; init; } = [];
        public string[] SettingInventory { get; init; } = [];
        public Case[] Cases { get; init; } = [];
    }

    private class Outcome
    {
        public string Status { get; init; } = "";
        public OracleCommand[] Commands { get; init; } = [];
        public OracleError[] Errors { get; init; } = [];
    }

    private sealed class Case : Outcome
    {
        public string Id { get; init; } = "";
        public string Path { get; init; } = "";
        public string Text { get; init; } = "";
        public string InputSha256 { get; init; } = "";
        public OracleToken[] Tokens { get; init; } = [];
        public Outcome? Syntax { get; init; }
        public Dictionary<string, string> Files { get; init; } = [];
    }

    private sealed record OracleCommand(string Type, string Options, string Args, string Source);

    private sealed record OracleToken
    {
        public string Type { get; init; } = "";
        public string Literal { get; init; } = "";
        public int Line { get; init; }
        public int Column { get; init; }
    }

    private sealed class OracleError
    {
        public OracleToken Token { get; init; } = new();
        public string Message { get; init; } = "";
    }

    private sealed class Provenance
    {
        public string Ref { get; init; } = "";
        public ProvenanceFile[] Files { get; init; } = [];
    }

    private sealed class ProvenanceFile
    {
        public string Path { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public string Ref { get; init; } = "";
        public int Bytes { get; init; }
    }
}
