using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests;

[TestClass]
public class TapeParserTests
{
    [TestMethod]
    public void Parse_AllCommandFamilies_ReturnsTypedInstructionsWithoutSideEffects()
    {
        var document = new TapeParser().Parse("""
            Source 'does-not-exist.tape'
            Output 'does-not-exist.unknown'
            Screenshot 'does-not-exist.png'
            Require 'does-not-exist-executable'
            Env 'HEX1B_TAPE_PARSER_TEST' 'not-applied'
            Type@.1 'hello' '世界'
            Copy 'clipboard remains untouched'
            Paste Hide Show Sleep 1m
            Wait+Screen@1m /(?i)ready/
            Ctrl+Alt+Shift+x+y Alt+word Shift+Tab
            """, "test.tape");

        Assert.AreEqual("test.tape", document.SourceName);
        Assert.AreEqual(15, document.Commands.Count);
        Assert.AreEqual("does-not-exist.tape", TestSeq.IsType<TapeSourceCommand>(document.Commands[0]).Path);
        Assert.AreEqual("does-not-exist.unknown", TestSeq.IsType<TapeOutputCommand>(document.Commands[1]).Path);
        Assert.AreEqual("does-not-exist.png", TestSeq.IsType<TapeScreenshotCommand>(document.Commands[2]).Path);
        Assert.AreEqual("does-not-exist-executable", TestSeq.IsType<TapeRequireCommand>(document.Commands[3]).Program);
        Assert.AreEqual("not-applied", TestSeq.IsType<TapeEnvCommand>(document.Commands[4]).Value);
        Assert.AreNotEqual("not-applied", Environment.GetEnvironmentVariable("HEX1B_TAPE_PARSER_TEST"));
        var type = TestSeq.IsType<TapeTypeCommand>(document.Commands[5]);
        Assert.AreEqual("hello 世界", type.Text);
        Assert.AreEqual(".1s", type.Delay!.Value);
        TestSeq.IsType<TapeCopyCommand>(document.Commands[6]);
        TestSeq.IsType<TapePasteCommand>(document.Commands[7]);
        TestSeq.IsType<TapeHideCommand>(document.Commands[8]);
        TestSeq.IsType<TapeShowCommand>(document.Commands[9]);
        Assert.AreEqual("1m", TestSeq.IsType<TapeSleepCommand>(document.Commands[10]).Duration.Value);
        var wait = TestSeq.IsType<TapeWaitCommand>(document.Commands[11]);
        Assert.AreEqual(TapeWaitScope.Screen, wait.Scope);
        Assert.AreEqual("(?i)ready", wait.Pattern);
        Assert.AreEqual("1m", wait.Timeout!.Value);
        var chord = TestSeq.IsType<TapeChordCommand>(document.Commands[12]);
        Assert.AreEqual("Ctrl", chord.Modifier);
        TestSeq.AreEqual(new[] { "Alt", "Shift", "x", "y" }, chord.Parts);
        Assert.AreEqual("word", TestSeq.IsType<TapeChordCommand>(document.Commands[13]).Parts[0]);
        Assert.AreEqual("Tab", TestSeq.IsType<TapeChordCommand>(document.Commands[14]).Parts[0]);
    }

    [TestMethod]
    [DataRow("Backspace")]
    [DataRow("Delete")]
    [DataRow("Insert")]
    [DataRow("Enter")]
    [DataRow("Escape")]
    [DataRow("Tab")]
    [DataRow("Space")]
    [DataRow("Up")]
    [DataRow("Down")]
    [DataRow("Left")]
    [DataRow("Right")]
    [DataRow("PageUp")]
    [DataRow("PageDown")]
    [DataRow("ScrollUp")]
    [DataRow("ScrollDown")]
    public void Parse_AllKeyCommands_PreservesCountAndDelay(string key)
    {
        var document = new TapeParser().Parse($"{key}@1.2.3 ms 2.5 {key}");
        var explicitKey = TestSeq.IsType<TapeKeyCommand>(document.Commands[0]);
        Assert.AreEqual(key, explicitKey.Key);
        Assert.AreEqual("1.2.3ms", explicitKey.Delay!.Value);
        Assert.AreEqual("1.2.3 ms", explicitKey.Delay.RawText);
        Assert.AreEqual("2.5", explicitKey.Count!.Value);
        var defaultKey = TestSeq.IsType<TapeKeyCommand>(document.Commands[1]);
        Assert.IsNull(defaultKey.Count);
        Assert.IsNull(defaultKey.Delay);
    }

    [TestMethod]
    [DataRow("Shell", "nonsense")]
    [DataRow("FontFamily", "'arbitrary missing font'")]
    [DataRow("FontSize", "nonsense")]
    [DataRow("LetterSpacing", "1.2.3")]
    [DataRow("LineHeight", "true")]
    [DataRow("Framerate", "nonsense")]
    [DataRow("TypingSpeed", "'100ms'")]
    [DataRow("Theme", "{\"invalid\":}")]
    [DataRow("PlaybackSpeed", "nonsense")]
    [DataRow("Height", "nonsense")]
    [DataRow("Width", "nonsense")]
    [DataRow("Padding", "nonsense")]
    [DataRow("LoopOffset", "'20%'")]
    [DataRow("MarginFill", "'#ABC123'")]
    [DataRow("Margin", "nonsense")]
    [DataRow("WindowBar", "'ColorfulRight'")]
    [DataRow("WindowBarSize", "nonsense")]
    [DataRow("BorderRadius", "nonsense")]
    [DataRow("CursorBlink", "false")]
    [DataRow("WaitTimeout", "0")]
    [DataRow("WaitPattern", "'(?P<name>a)'")]
    public void Parse_AllSettings_RecognizesSyntaxWithoutRuntimeConversion(string setting, string operand)
    {
        var command = TestSeq.IsType<TapeSetCommand>(TestSeq.Single(new TapeParser().Parse($"Set {setting} {operand}").Commands));
        Assert.AreEqual(Enum.Parse<TapeSetting>(setting), command.Setting);
        Assert.AreEqual(operand, command.Value.RawText);
    }

    [TestMethod]
    [DataRow("Set TypingSpeed '100ms'", "100mss", TapeTokenKind.String)]
    [DataRow("Set TypingSpeed nonsense", "nonsenses", TapeTokenKind.String)]
    [DataRow("Set LoopOffset '20%'", "20%%", TapeTokenKind.String)]
    [DataRow("Set LoopOffset 20 %", "20%", TapeTokenKind.Number)]
    [DataRow("Set Width", "\0", TapeTokenKind.EndOfFile)]
    [DataRow("Set Width Enter", "Enter", TapeTokenKind.Keyword)]
    [DataRow("Set Theme {unclosed", "{unclosed}", TapeTokenKind.Json)]
    [DataRow("Set WaitTimeout 1.2.3", "1.2.3s", TapeTokenKind.Number)]
    public void Parse_PermissiveSettingOperands_PreservesNormalizedValue(string text, string value, TapeTokenKind kind)
    {
        var command = TestSeq.IsType<TapeSetCommand>(TestSeq.Single(new TapeParser().Parse(text).Commands));
        Assert.AreEqual(value, command.Value.Value);
        Assert.AreEqual(kind, command.Value.Kind);
    }

    [TestMethod]
    [DataRow("Type 'a''b'", "a b")]
    [DataRow("Type a\n\tb", "a b")]
    [DataRow("Type 'a  b'", "a  b")]
    [DataRow("Type '\\n\\t\\u1234'", "\\n\\t\\u1234")]
    [DataRow("Type `a\"b'c`", "a\"b'c")]
    [DataRow("Type 'unterminated", "unterminated")]
    [DataRow("Type 'one\ntwo", "one two")]
    [DataRow("Type '😀é世界'", "😀é世界")]
    [DataRow("Type Home", "Home")]
    [DataRow("Type './relative/path'", "./relative/path")]
    public void Parse_StringLexing_FollowsUpstreamLiteralRules(string text, string expected)
    {
        Assert.AreEqual(expected, TestSeq.IsType<TapeTypeCommand>(TestSeq.Single(new TapeParser().Parse(text).Commands)).Text);
    }

    [TestMethod]
    [DataRow("Sleep 1", "1s")]
    [DataRow("Sleep .1", ".1s")]
    [DataRow("Sleep 1.2.3", "1.2.3s")]
    [DataRow("Sleep 1.", "1.s")]
    [DataRow("Sleep 0", "0s")]
    [DataRow("Sleep 2 m", "2m")]
    [DataRow("Sleep 10\nms", "10ms")]
    public void Parse_Durations_PreservesUpstreamAcceptedValues(string text, string value)
    {
        Assert.AreEqual(value, TestSeq.IsType<TapeSleepCommand>(TestSeq.Single(new TapeParser().Parse(text).Commands)).Duration.Value);
    }

    [TestMethod]
    [DataRow("Ctrl+Alt")]
    [DataRow("Ctrl+Shift")]
    [DataRow("Ctrl+'Alt'+x")]
    [DataRow("Ctrl+'1'")]
    [DataRow("Ctrl+@")]
    [DataRow("Ctrl+\\")]
    [DataRow("Ctrl+^")]
    [DataRow("Ctrl+Left+Right")]
    [DataRow("Alt+word")]
    [DataRow("Alt+Tab")]
    [DataRow("Shift+'unicodeé'")]
    [DataRow("Shift+[")]
    [DataRow("Ctrl+Alt+Alt+x")]
    public void Parse_UpstreamModifierForms_Accepts(string text)
    {
        Assert.IsTrue(new TapeParser().TryParse(text, out _));
    }

    [TestMethod]
    [DataRow("type hello")]
    [DataRow("Home")]
    [DataRow("End")]
    [DataRow("Type Enter")]
    [DataRow("Type true")]
    [DataRow("Type ms")]
    [DataRow("Type é")]
    [DataRow("Type _word")]
    [DataRow("Type")]
    [DataRow("Copy@1s 'text'")]
    [DataRow("Sleep -1")]
    [DataRow("Sleep '1s'")]
    [DataRow("Sleep 1h")]
    [DataRow("Sleep 1s500ms")]
    [DataRow("Ctrl+1")]
    [DataRow("Ctrl+é")]
    [DataRow("Ctrl+'é'")]
    [DataRow("Ctrl+Tab")]
    [DataRow("Ctrl+Escape")]
    [DataRow("Ctrl+x+Alt")]
    [DataRow("Alt+Left")]
    [DataRow("Shift+Space")]
    [DataRow("Ctrl")]
    [DataRow("Alt")]
    [DataRow("Shift")]
    [DataRow("Wait 'pattern'")]
    [DataRow("Wait+line")]
    [DataRow("Wait@0")]
    [DataRow("Wait@1.2.3")]
    [DataRow("Wait@9223372037s")]
    [DataRow("Wait@9223372036.854775808s")]
    [DataRow("Wait@0.0000000001")]
    [DataRow("Set TypingSpeed 1m")]
    [DataRow("Set Width 10px")]
    [DataRow("Set CursorBlink 'true'")]
    [DataRow("Set WindowBar invalid")]
    [DataRow("Set MarginFill '#123'")]
    [DataRow("Set MarginFill '#GGGGGG'")]
    [DataRow("Set Unknown 1")]
    [DataRow("Output no-extension")]
    [DataRow("Source source.txt")]
    [DataRow("Source source.TAPE")]
    [DataRow("Screenshot screenshot.gif")]
    [DataRow("Screenshot screenshot.PNG")]
    [DataRow("Require 123")]
    [DataRow("Env KEY 123")]
    [DataRow("Env KEY false")]
    [DataRow("Type # operand interrupted")]
    [DataRow("SlowType 'not a built-in'")]
    public void TryParse_InvalidSyntax_ReturnsNullAndParsingErrors(string text)
    {
        var success = new TapeParser().TryParse(text, out var document, out var diagnostics, "invalid.tape");
        Assert.IsFalse(success, text);
        Assert.IsNull(document);
        Assert.IsTrue(diagnostics.Count > 0);
        Assert.IsTrue(diagnostics.All(d => d.Stage == TapeDiagnosticStage.Parsing));
        Assert.IsTrue(diagnostics.All(d => d.Span.SourceName == "invalid.tape"));
    }

    [TestMethod]
    [DataRow("Output file.")]
    [DataRow("Output .hidden")]
    [DataRow("Output frames/")]
    [DataRow("Output file.MP4")]
    [DataRow("Output file.unrecognized")]
    [DataRow("Output '/absolute/file.gif'")]
    [DataRow("Env '' 'value'")]
    [DataRow("Env 'KEY=INVALID' 'value'")]
    [DataRow("Env 123 'value'")]
    [DataRow("Env Enter 'value'")]
    [DataRow("Require 'git curl'")]
    [DataRow("Set WaitPattern")]
    [DataRow("Set MarginFill arbitrary")]
    [DataRow("Wait //")]
    [DataRow("Wait")]
    [DataRow("Wait+Line")]
    [DataRow("Wait@1.000000000000000000000000000000000000000000s")]
    [DataRow("Wait@9223372036.854775807s")]
    [DataRow("Wait@0.000000001s")]
    [DataRow("Wait@.1ms")]
    public void Parse_UpstreamPermissiveForms_Accepts(string text)
    {
        Assert.IsTrue(new TapeParser().TryParse(text, out _, out var diagnostics), string.Join("; ", diagnostics.Select(d => d.Message)));
    }

    [TestMethod]
    public void Parse_CommentsAndNul_FollowsCommandBoundaries()
    {
        var document = new TapeParser().Parse("# first\nType one # separate\nType two\0Unknown");
        Assert.AreEqual(2, document.Commands.Count);
        Assert.AreEqual("one", TestSeq.IsType<TapeTypeCommand>(document.Commands[0]).Text);
        Assert.AreEqual("two", TestSeq.IsType<TapeTypeCommand>(document.Commands[1]).Text);
    }

    [TestMethod]
    public void Parse_SourceSpans_PreserveOffsetsRawTextAndSource()
    {
        const string text = "# comment\r\nType@1 ms '😀' Enter";
        var document = new TapeParser().Parse(text, "spans.tape");
        var type = TestSeq.IsType<TapeTypeCommand>(document.Commands[0]);
        Assert.AreEqual(new TapeSourceSpan("spans.tape", 11, 14, 2, 1), type.Span);
        Assert.AreEqual("Type@1 ms '😀'", text.Substring(type.Span.Offset, type.Span.Length));
        Assert.AreEqual("1 ms", type.Delay!.RawText);
        Assert.AreEqual(18, document.Commands[1].Span.Column);
    }

    [TestMethod]
    public void Parse_MultipleErrors_ThrowsImmutableDiagnosticSnapshot()
    {
        var exception = Assert.ThrowsExactly<TapeParseException>(() => new TapeParser().Parse("Unknown\nOther", "bad.tape"));
        Assert.AreEqual(2, exception.Diagnostics.Count);
        Assert.AreEqual(2, exception.Diagnostics[1].Span.Line);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<TapeDiagnostic>)exception.Diagnostics).Clear());
    }

    [TestMethod]
    public void Document_MutableInputs_CopiesInitialCommands()
    {
        var span = new TapeSourceSpan("test", 0, 4, 1, 1);
        var commands = new List<TapeCommand> { new TapeHideCommand(span) };
        var document = new TapeDocument(commands, "test");
        commands.Clear();
        Assert.AreEqual(1, document.Commands.Count);
        Assert.AreEqual("test", document.SourceName);
        var parts = new List<string> { "x" };
        var chord = new TapeChordCommand("Ctrl", parts, span);
        parts.Clear();
        Assert.AreEqual(1, chord.Parts.Count);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<string>)chord.Parts).Clear());
    }

    [TestMethod]
    public void Document_Commands_CanAddReplaceInsertRemoveAndClear()
    {
        var document = new TapeDocument();
        var hide = new TapeHideCommand(default);
        var show = new TapeShowCommand(default);
        Assert.IsEmpty(document.Commands);
        document.Commands.Add(hide);
        document.Commands[0] = show;
        document.Commands.Insert(0, hide);
        TestSeq.AreEqual<TapeCommand>([hide, show], document.Commands);
        Assert.IsTrue(document.Commands.Remove(hide));
        Assert.AreSame(show, TestSeq.Single(document.Commands));
        document.Commands.RemoveAt(0);
        Assert.IsEmpty(document.Commands);
        document.Commands.Add(hide);
        document.Commands.Clear();
        Assert.IsEmpty(document.Commands);
    }
}
