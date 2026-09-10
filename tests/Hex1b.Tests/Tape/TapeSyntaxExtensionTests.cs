using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests;

[TestClass]
public class TapeSyntaxExtensionTests
{
    [TestMethod]
    public void Parse_RegisteredSyntax_ProducesExtensionCommandWithSourceContext()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Note", ParseNote);
        var parser = new TapeParser(options);
        options.SyntaxExtensions.Add("LaterNote", ParseNote);
        Assert.IsFalse(parser.TryParse("LaterNote 'late'", out _));
        Assert.IsTrue(new TapeParser(options).TryParse("LaterNote 'late'", out _));
        var document = parser.Parse("Note 'hello' Enter", "custom.tape");
        var note = TestSeq.IsType<TapeExtensionCommand>(document.Commands[0]);
        Assert.AreEqual("Note", note.Keyword);
        Assert.AreEqual("custom.tape", note.Span.SourceName);
        Assert.AreEqual(0, note.Span.Offset);
        Assert.AreEqual(12, note.Span.Length);
        TestSeq.IsType<TapeKeyCommand>(document.Commands[1]);
        Assert.AreSame(parser, document.Parser);
        document.Commands.RemoveAt(1);
        document.Commands.Add(parser.Parse("Note 'added'").Commands[0]);
        Assert.AreSame(parser, document.Parser);
        Assert.AreEqual("custom.tape", document.SourceName);
        Assert.AreEqual("Note", TestSeq.IsType<TapeExtensionCommand>(document.Commands[1]).Keyword);
    }

    [TestMethod]
    public void Parse_ContextReader_ConsumesOperandsAndPreservesFollowingCommands()
    {
        const string text = "Note 'hello' Enter Note 'again'";
        var operands = new List<string>();
        var offsets = new List<int>();
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Note"] = parse =>
        {
            Assert.AreEqual("context.tape", parse.SourceName);
            Assert.AreEqual("Note", text.Substring(parse.CommandSpan.Offset, parse.CommandSpan.Length));
            var operand = parse.Reader.Current;
            Assert.AreEqual(TapeTokenKind.String, operand.Kind);
            var following = parse.Reader.Peek(1);
            Assert.AreEqual(operand, parse.Reader.Current);
            Assert.AreEqual(operand, parse.Reader.Read());
            Assert.AreEqual(following, parse.Reader.Current);
            Assert.AreEqual("context.tape", operand.Span.SourceName);
            operands.Add(operand.Value);
            offsets.Add(parse.CommandSpan.Offset);
            return _ => throw new AssertFailedException("Parsing must not compile.");
        };

        var document = new TapeParser(options).Parse(text, "context.tape");

        TestSeq.AreEqual(["hello", "again"], operands);
        TestSeq.AreEqual([0, 19], offsets);
        Assert.AreEqual(3, document.Commands.Count);
        Assert.AreEqual(12, document.Commands[0].Span.Length);
        TestSeq.IsType<TapeKeyCommand>(document.Commands[1]);
        Assert.AreEqual(19, document.Commands[2].Span.Offset);
        Assert.AreEqual(12, document.Commands[2].Span.Length);
    }

    [TestMethod]
    [DataRow("literal")]
    [DataRow("Custom")]
    public void Parse_TypeBeforeRegisteredKeyword_PreservesCommandBoundaryAndQuotedText(string text)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Custom"] = ParseNote;
        var parser = new TapeParser(options);

        var document = parser.Parse($"Type '{text}' Custom 'operand'");

        Assert.AreEqual(2, document.Commands.Count);
        Assert.AreEqual(text, TestSeq.IsType<TapeTypeCommand>(document.Commands[0]).Text);
        Assert.AreEqual("Custom", TestSeq.IsType<TapeExtensionCommand>(document.Commands[1]).Keyword);
        Assert.AreEqual("Custom", TestSeq.IsType<TapeTypeCommand>(TestSeq.Single(parser.Parse("Type 'Custom'").Commands)).Text);
    }

    [TestMethod]
    [DataRow("Custom", TapeTokenKind.Keyword)]
    [DataRow("'Custom'", TapeTokenKind.String)]
    [DataRow("\"Custom\"", TapeTokenKind.String)]
    [DataRow("`Custom`", TapeTokenKind.String)]
    public void Parse_ReaderRegisteredKeyword_OnlyClassifiesBareWordsAsKeywords(string operand, TapeTokenKind expectedKind)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Custom"] = ParseNote;
        options.SyntaxExtensions["Inspect"] = parse =>
        {
            Assert.AreEqual(expectedKind, parse.Reader.Current.Kind);
            var token = parse.Reader.Read();
            Assert.AreEqual(expectedKind, token.Kind);
            Assert.AreEqual("Custom", token.Value);
            return _ => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder());
        };

        Assert.AreEqual(1, new TapeParser(options).Parse($"Inspect {operand}").Commands.Count);
    }

    [TestMethod]
    [DataRow("Enter")]
    [DataRow("Source 'missing.tape'")]
    [DataRow("Output 'capture.txt'")]
    public void Parse_TypeBeforeRemovedBuiltin_RejectsRatherThanConsumingKeywordAsText(string removedCommand)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Remove(removedCommand.Split(' ')[0]);

        Assert.IsFalse(new TapeParser(options).TryParse($"Type 'literal' {removedCommand}",
            out var document, out var diagnostics));

        Assert.IsNull(document);
        Assert.IsTrue(diagnostics.Any(d => d.Stage == TapeDiagnosticStage.Parsing));
    }

    [TestMethod]
    public void TryParse_ExtensionDiagnostic_ReturnsFailureWithoutPartialDocument()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Note", ParseNote);
        var parser = new TapeParser(options);
        Assert.IsFalse(parser.TryParse("Note Enter", out var document, out var diagnostics, "invalid.tape"));
        Assert.IsNull(document);
        var diagnostic = TestSeq.Single(diagnostics);
        Assert.AreEqual("NOTE001", diagnostic.Code);
        Assert.AreEqual("invalid.tape", diagnostic.Span.SourceName);
        Assert.AreEqual(5, diagnostic.Span.Offset);
        Assert.AreEqual(5, diagnostic.Span.Length);
    }

    [TestMethod]
    public void TryParse_ExtensionImplementationFailure_DoesNotSwallowException()
    {
        var options = new TapeParserOptions();
        var failure = new InvalidOperationException("extension failed");
        options.SyntaxExtensions.Add("Failure", _ => throw failure);
        var parser = new TapeParser(options);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => parser.TryParse("Failure", out _));
        Assert.AreSame(failure, error);
    }

    [TestMethod]
    [DataRow("Shell")]
    [DataRow("true")]
    [DataRow("ms")]
    [DataRow("two words")]
    [DataRow("'quoted'")]
    [DataRow("\"quoted\"")]
    [DataRow("`quoted`")]
    [DataRow(" note")]
    [DataRow("note ")]
    [DataRow("@")]
    [DataRow("1number")]
    [DataRow("")]
    public void Constructor_InvalidExtensionKeyword_RejectsOptions(string keyword)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add(keyword, ParseNote);
        Assert.Throws<ArgumentException>(() => new TapeParser(options));
    }

    [TestMethod]
    public void SyntaxExtensions_AddDuplicateKeyword_RejectsWithoutReplacing()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Note", ParseNote);
        Assert.ThrowsExactly<ArgumentException>(() =>
            options.SyntaxExtensions.Add("Note", _ => throw new AssertFailedException("A duplicate replaced the original parser.")));
        var parser = new TapeParser(options);
        Assert.AreEqual("Note", TestSeq.IsType<TapeExtensionCommand>(TestSeq.Single(parser.Parse("Note 'kept'").Commands)).Keyword);
    }

    [TestMethod]
    public void SyntaxExtensions_IndexerReplacementAndRemoval_LeaveExistingParserUnchanged()
    {
        var options = new TapeParserOptions
        {
            SyntaxExtensions =
            {
                ["Note"] = ParseNote
            }
        };
        var original = new TapeParser(options);
        var failure = new InvalidOperationException("replacement parser");
        options.SyntaxExtensions["Note"] = _ => throw failure;
        var replaced = new TapeParser(options);

        Assert.IsTrue(original.TryParse("Note 'original'", out _));
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => replaced.Parse("Note")));
        Assert.IsTrue(options.SyntaxExtensions.Remove("Note"));
        Assert.IsFalse(new TapeParser(options).TryParse("Note 'removed'", out _));
        Assert.IsTrue(original.TryParse("Note 'still available'", out _));
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => replaced.Parse("Note")));
    }

    [TestMethod]
    public void SyntaxExtensions_OptionsInstances_DoNotShareCallbacks()
    {
        var first = new TapeParserOptions();
        var second = new TapeParserOptions();
        first.SyntaxExtensions.Add("Note", ParseNote);
        Assert.IsTrue(first.SyntaxExtensions.Remove("Type"));
        first.SyntaxExtensions["Source"] = ParseNote;

        Assert.IsFalse(second.SyntaxExtensions.ContainsKey("Note"));
        Assert.IsTrue(second.SyntaxExtensions.ContainsKey("Type"));
        Assert.IsTrue(new TapeParser(second).TryParse("Type 'default'", out _));
        TestSeq.IsType<TapeSourceCommand>(TestSeq.Single(new TapeParser(second).Parse("Source 'default.tape'").Commands));
        Assert.IsFalse(new TapeParser(second).TryParse("Note 'unregistered'", out _));
    }

    [TestMethod]
    [DataRow("Type", "Type 'text'")]
    [DataRow("Set", "Set TypingSpeed 0")]
    [DataRow("Source", "Source 'missing.tape'")]
    [DataRow("Output", "Output 'capture.txt'")]
    [DataRow("Enter", "Enter")]
    [DataRow("Ctrl", "Ctrl+a")]
    [DataRow("End", "End")]
    public void SyntaxExtensions_RemoveBuiltin_RejectsParsingWithoutChangingExistingParser(string keyword, string source)
    {
        var options = new TapeParserOptions();
        Assert.IsTrue(options.SyntaxExtensions.ContainsKey(keyword));
        var original = new TapeParser(options);
        var wasAccepted = original.TryParse(source, out _);

        Assert.IsTrue(options.SyntaxExtensions.Remove(keyword));
        var restricted = new TapeParser(options);

        Assert.IsFalse(restricted.TryParse(source, out var document, out var diagnostics));
        Assert.IsNull(document);
        Assert.IsTrue(diagnostics.Any(d => d.Stage == TapeDiagnosticStage.Parsing));
        Assert.AreEqual(wasAccepted, original.TryParse(source, out _));
    }

    [TestMethod]
    public void SyntaxExtensions_ClearBuiltins_RetainsOnlyAddedSyntaxInNewParser()
    {
        var options = new TapeParserOptions();
        var original = new TapeParser(options);
        options.SyntaxExtensions.Clear();
        options.SyntaxExtensions["Note"] = ParseNote;
        var restricted = new TapeParser(options);

        Assert.IsFalse(restricted.TryParse("Type 'text'", out _));
        Assert.IsFalse(restricted.TryParse("Enter", out _));
        Assert.IsFalse(restricted.TryParse("Source 'missing.tape'", out _));
        TestSeq.IsType<TapeExtensionCommand>(TestSeq.Single(restricted.Parse("Note 'available'").Commands));
        Assert.IsTrue(original.TryParse("Type 'text' Enter Source 'missing.tape'", out _));
        Assert.IsFalse(original.TryParse("Note 'unavailable'", out _));
    }

    [TestMethod]
    [DataRow("Type")]
    [DataRow("Source")]
    [DataRow("End")]
    public void SyntaxExtensions_ReplaceBuiltin_UsesReplacementGrammar(string keyword)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions[keyword] = ParseNote;

        var command = TestSeq.IsType<TapeExtensionCommand>(
            TestSeq.Single(new TapeParser(options).Parse($"{keyword} 'replacement'").Commands));

        Assert.AreEqual(keyword, command.Keyword);
    }

    [TestMethod]
    [DataRow("Set TypingSpeed 0")]
    [DataRow("Sleep 0")]
    [DataRow("Type 'text'")]
    [DataRow("Enter")]
    [DataRow("Ctrl+a")]
    [DataRow("Hide")]
    [DataRow("Show")]
    [DataRow("Require 'dotnet'")]
    [DataRow("Output 'capture.txt'")]
    [DataRow("Wait /ready/")]
    [DataRow("Source 'include.tape'")]
    [DataRow("Screenshot 'capture.png'")]
    [DataRow("Copy 'text'")]
    [DataRow("Paste")]
    [DataRow("Env NAME 'value'")]
    public void Parse_DefaultBuiltin_StoresDeferredPreparationCallback(string source)
    {
        var command = TestSeq.Single(new TapeParser().Parse(source).Commands);

        Assert.IsNotInstanceOfType<TapeExtensionCommand>(command);
        Assert.IsNotNull(command.PrepareCallback);
    }

    [TestMethod]
    public void Parse_ExtensionLookahead_DoesNotConsumeTokens()
    {
        var reader = new TapeTokenReader("one 'two' 3", "tokens");
        Assert.AreEqual("one", reader.Current.Value);
        Assert.AreEqual("two", reader.Peek(1).Value);
        Assert.AreEqual("3", reader.Peek(2).Value);
        Assert.AreEqual(TapeTokenKind.EndOfFile, reader.Peek(10).Kind);
        Assert.AreEqual("one", reader.Read().Value);
        Assert.AreEqual("'two'", reader.Read().RawText);
        Assert.AreEqual(TapeTokenKind.Number, reader.Read().Kind);
        Assert.AreEqual(TapeTokenKind.EndOfFile, reader.Read().Kind);
        Assert.AreEqual(TapeTokenKind.EndOfFile, reader.Read().Kind);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.Peek(-1));
    }

    [TestMethod]
    public async Task Validate_SourceAddedAfterParsing_RetainsSyntaxExtensions()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-edit-{Guid.NewGuid():N}"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "include.tape"), "Type 'builtin' Note 'included'");
            var options = new TapeParserOptions();
            var compiled = new List<string>();
            var sources = new List<string?>();
            options.SyntaxExtensions.Add("Note", parse =>
            {
                var text = parse.Reader.Read().Value;
                sources.Add(parse.SourceName);
                return context =>
                {
                    compiled.Add(text);
                    return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
                };
            });
            var parser = new TapeParser(options);
            options.SyntaxExtensions.Clear();
            var document = parser.Parse("Note 'root'", "edited.tape");
            document.Commands.Add(new TapeSourceCommand("include.tape", default));
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();

            var validation = await new TapePlayer().ValidateAsync(document, terminal, new TapePlaybackOptions
            {
                WorkingDirectory = directory.FullName
            });

            Assert.IsTrue(validation.CanExecute);
            TestSeq.AreEqual(["root", "included"], compiled);
            TestSeq.AreEqual(["edited.tape", Path.Combine(directory.FullName, "include.tape")], sources);
            Assert.AreEqual("edited.tape", document.SourceName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void SyntaxExtensions_NullKey_RejectsAddition()
    {
        var options = new TapeParserOptions();
        Assert.ThrowsExactly<ArgumentNullException>(() => options.SyntaxExtensions.Add(null!, ParseNote));
    }

    [TestMethod]
    public void Constructor_NullParserCallback_RejectsOptions()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Note"] = null!;
        var error = Assert.ThrowsExactly<ArgumentException>(() => new TapeParser(options));
        Assert.IsTrue(error.Message.Contains("Note"));
    }

    [TestMethod]
    public void Parse_DeferredPlaybackCallback_IsNotInvoked()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Deferred", _ => _ => throw new AssertFailedException("Parsing must not compile."));
        var parser = new TapeParser(options);
        var document = parser.Parse("Deferred Deferred");
        Assert.AreEqual(2, document.Commands.Count);
        Assert.IsTrue(document.Commands.All(command => command is TapeExtensionCommand { Keyword: "Deferred" }));
    }

    [TestMethod]
    public void TryParse_NullPlaybackCallback_ThrowsImplementationError()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Note", _ => null!);
        var parser = new TapeParser(options);
        Assert.ThrowsExactly<InvalidOperationException>(() => parser.TryParse("Note", out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Validate_InvalidCallbackResult_PropagatesImplementationError(bool returnNull)
    {
        var options = new TapeParserOptions();
        var failure = new InvalidOperationException("compiler failed");
        options.SyntaxExtensions.Add("Failure", _ => _ => returnNull ? null! : throw failure);
        var parser = new TapeParser(options);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new TapePlayer().ValidateAsync(parser.Parse("Failure")));
        if (returnNull)
            Assert.IsTrue(error.Message.Contains("Failure"));
        else
            Assert.AreSame(failure, error);
    }

    [TestMethod]
    public void Parse_KeywordAliasesAndCase_MapToRegisteredCallbacks()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Note", ParseNote);
        options.SyntaxExtensions.Add("Remark", ParseNote);
        options.SyntaxExtensions.Add("note", _ => context => TapeCommandResult.Accept(
            new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions)));
        var parser = new TapeParser(options);
        var document = parser.Parse("Note 'first' Remark 'second' note");

        TestSeq.AreEqual(["Note", "Remark", "note"], document.Commands.Cast<TapeExtensionCommand>().Select(command => command.Keyword));
        Assert.IsFalse(parser.TryParse("NOTE 'unknown'", out _));
        Assert.IsFalse(parser.TryParse("'Note' 'quoted keyword'", out _));
        Assert.IsFalse(new TapeParser().TryParse("Note 'unregistered'", out _));
    }

    [TestMethod]
    public async Task Play_CallbackExtension_ExecutesAndCapturesAsOneCommand()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-extension-{Guid.NewGuid():N}"));
        try
        {
            var options = new TapeParserOptions();
            options.SyntaxExtensions["ClickReady"] = _ => context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
                .WithOptions(context.SequenceOptions)
                .WaitUntil(snapshot => snapshot.ContainsText("Ready"), context.WaitTimeout)
                .Enter()
                .WaitUntil(snapshot => snapshot.ContainsText("Clicked"), context.WaitTimeout));
            var parser = new TapeParser(options);
            var clicked = false;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using var result = await new TapePlayer().PlayAsync(parser.Parse("ClickReady"), new TapePlaybackOptions
            {
                Capture = new TapeCaptureOptions { GoldenTextPath = Path.Combine(directory.FullName, "extension.txt") },
                TerminalFactory = builder => builder.WithDimensions(30, 5)
                    .WithHex1bApp(context => context.Button(clicked ? "Clicked" : "Ready").OnClick(_ => { clicked = true; }))
                    .Build()
            }, cancellation.Token);

            Assert.IsTrue(clicked);
            Assert.IsTrue(result.FinalSnapshot.ContainsText("Clicked"));
            Assert.AreEqual(1, result.CompletedCommandCount);
            var golden = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "extension.txt"));
            Assert.IsTrue(golden.Contains("Clicked"));
            Assert.AreEqual(1, golden.Split(new string('\u2500', 80), StringSplitOptions.None).Length - 1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task ValidateThenPlay_CallbackOperandsAndTiming_PreparesWithoutExecutingUntilPlayback()
    {
        var parsed = 0;
        var compiled = new List<(string Text, TimeSpan TypingSpeed)>();
        var executed = new List<string>();
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Step", parse =>
        {
            parsed++;
            var text = parse.Reader.Read().Value;
            return context =>
            {
                compiled.Add((text, context.TypingSpeed));
                return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions)
                    .WaitUntil(_ => { executed.Add(text); return true; }, context.WaitTimeout));
            };
        });
        var parser = new TapeParser(options);
        var document = parser.Parse("Set TypingSpeed 10ms Step 'one' Set TypingSpeed 20ms Step 'two'");
        Assert.AreEqual(2, parsed);
        Assert.IsEmpty(compiled);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        var player = new TapePlayer();

        Assert.IsTrue((await player.ValidateAsync(document, terminal)).CanExecute);
        TestSeq.AreEqual([("one", TimeSpan.FromMilliseconds(10)), ("two", TimeSpan.FromMilliseconds(20))], compiled);
        Assert.IsEmpty(executed);
        using var result = await player.PlayAsync(document, terminal);
        TestSeq.AreEqual(["one", "two"], executed);
        Assert.AreEqual(4, compiled.Count);
        Assert.AreEqual(2, parsed);
    }

    [TestMethod]
    public async Task Validate_CallbackReturnsError_ReportsCompilationDiagnosticWithCommandSpan()
    {
        var parserOptions = new TapeParserOptions();
        parserOptions.SyntaxExtensions.Add("Blocked", parse =>
        {
            parse.Reader.Read();
            return _ => TapeCommandResult.Reject("This operation is disabled.");
        });
        var document = new TapeParser(parserOptions).Parse("\nBlocked 'operand'", "blocked.tape");
        var player = new TapePlayer();

        var validation = await player.ValidateAsync(document);
        Assert.IsFalse(validation.CanExecute);
        var diagnostic = TestSeq.Single(validation.Diagnostics);
        Assert.AreEqual("TAPE_COMMAND", diagnostic.Code);
        Assert.AreEqual(TapeDiagnosticStage.Compilation, diagnostic.Stage);
        Assert.AreEqual(TapeDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.AreEqual("This operation is disabled.", diagnostic.Message);
        Assert.AreEqual(document.Commands[0].Span, diagnostic.Span);
        Assert.AreEqual("blocked.tape", diagnostic.Span.SourceName);
        Assert.AreEqual(1, diagnostic.Span.Offset);
        Assert.AreEqual(17, diagnostic.Span.Length);
        await Assert.ThrowsAsync<TapeValidationException>(() => player.PlayAsync(document));
    }

    [TestMethod]
    public async Task Play_SyntaxReplacement_UsesReplacementCallback()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("Replaced", _ => _ => throw new AssertFailedException("The original callback must not run."));
        options.SyntaxExtensions["Replaced"] = parse =>
        {
            Assert.AreEqual(8, parse.CommandSpan.Length);
            return context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        };
        var document = new TapeParser(options).Parse("Replaced");
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();

        using var result = await new TapePlayer().PlayAsync(document, terminal);

        Assert.AreEqual(1, result.CompletedCommandCount);
    }

    [TestMethod]
    public async Task Play_ExtensionWaitFails_PreservesCommandAndSource()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Add("NeverReady", _ => context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(context.SequenceOptions)
            .WaitUntil(_ => false, TimeSpan.FromMilliseconds(30))));
        var document = new TapeParser(options).Parse("NeverReady", "failure.tape");
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var error = await Assert.ThrowsAsync<TapePlaybackException>(() =>
            new TapePlayer().PlayAsync(document, terminal, cancellationToken: cancellation.Token));

        Assert.AreSame(document.Commands[0], error.Command);
        Assert.AreEqual("failure.tape", error.Command.Span.SourceName);
        Assert.AreEqual("NeverReady", TestSeq.IsType<TapeExtensionCommand>(error.Command).Keyword);
        Assert.IsInstanceOfType<WaitUntilTimeoutException>(error.InnerException);
    }

    [TestMethod]
    public void CommandResult_AcceptAndReject_ExposeOnlyTheirRespectiveValues()
    {
        var builder = new Hex1bTerminalInputSequenceBuilder().Type("text");
        var accepted = TapeCommandResult.Accept(builder);
        var rejected = TapeCommandResult.Reject("command rejected");

        Assert.AreSame(builder, accepted.SequenceBuilder);
        Assert.IsNull(accepted.ErrorMessage);
        Assert.IsNull(rejected.SequenceBuilder);
        Assert.AreEqual("command rejected", rejected.ErrorMessage);
    }

    [TestMethod]
    public void CommandResult_NullValues_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TapeCommandResult.Accept(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TapeCommandResult.Reject(null!));
    }

    [TestMethod]
    public async Task Play_LaterCallbackMutatesEarlierBuilder_UsesPreparedSequenceSnapshot()
    {
        var executed = new List<string>();
        var first = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => { executed.Add("original"); return true; }, TimeSpan.FromSeconds(1));
        var options = new TapeParserOptions();
        options.SyntaxExtensions["First"] = _ => _ => TapeCommandResult.Accept(first);
        options.SyntaxExtensions["Mutate"] = _ => _ =>
        {
            first.WaitUntil(_ => { executed.Add("unexpected"); return true; }, TimeSpan.FromSeconds(1));
            return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(_ => { executed.Add("later"); return true; }, TimeSpan.FromSeconds(1)));
        };
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();

        using var result = await new TapePlayer().PlayAsync(new TapeParser(options).Parse("First Mutate"), terminal);

        TestSeq.AreEqual(["original", "later"], executed);
        Assert.AreEqual(2, result.CompletedCommandCount);
    }

    [TestMethod]
    [DataRow("''")]
    [DataRow("'   '")]
    [DataRow("123")]
    [DataRow("true")]
    [DataRow("")]
    public async Task ValidateAndPlay_DocumentedWaitForTextWithInvalidOperand_ReturnsCommandError(string operand)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["WaitForText"] = ParseWaitForText;
        var document = new TapeParser(options).Parse($"WaitForText {operand}", "documented.tape");
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        var player = new TapePlayer();

        var validation = await player.ValidateAsync(document, terminal);
        var failure = await Assert.ThrowsExactlyAsync<TapeValidationException>(() => player.PlayAsync(document, terminal));

        Assert.IsFalse(validation.CanExecute);
        var diagnostic = TestSeq.Single(validation.Diagnostics);
        Assert.AreEqual("TAPE_COMMAND", diagnostic.Code);
        Assert.AreEqual(TapeDiagnosticStage.Compilation, diagnostic.Stage);
        Assert.AreEqual("WaitForText requires a non-empty string.", diagnostic.Message);
        Assert.AreEqual(document.Commands[0].Span, diagnostic.Span);
        TestSeq.AreEqual(validation.Diagnostics, failure.Diagnostics);
    }

    [TestMethod]
    public async Task Play_DocumentedWaitForTextWithValidOperand_WaitsForRenderedText()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["WaitForText"] = ParseWaitForText;
        var document = new TapeParser(options).Parse("WaitForText 'Ready'");
        var factoryCalls = 0;
        var playback = new TapePlaybackOptions
        {
            TerminalFactory = builder =>
            {
                factoryCalls++;
                return builder.WithDimensions(30, 5)
                    .WithHex1bApp(context => context.Text("Ready"))
                    .Build();
            }
        };
        var player = new TapePlayer();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var validation = await player.ValidateAsync(document, playback, cancellation.Token);
        Assert.IsTrue(validation.CanExecute);
        Assert.AreEqual(0, factoryCalls);
        using var result = await player.PlayAsync(document, playback, cancellation.Token);

        Assert.IsTrue(result.FinalSnapshot.ContainsText("Ready"));
        Assert.AreEqual(1, factoryCalls);
        Assert.AreEqual(1, result.CompletedCommandCount);
    }

    private static Func<TapePlayContext, TapeCommandResult> ParseWaitForText(TapeParseContext parse)
    {
        var operand = parse.Reader.Read();
        return play =>
        {
            if (operand.Kind != TapeTokenKind.String ||
                string.IsNullOrWhiteSpace(operand.Value))
                return TapeCommandResult.Reject("WaitForText requires a non-empty string.");

            return TapeCommandResult.Accept(
                new Hex1bTerminalInputSequenceBuilder()
                    .WithOptions(play.SequenceOptions)
                    .WaitUntil(snapshot => snapshot.ContainsText(operand.Value),
                        play.WaitTimeout, $"Waiting for '{operand.Value}'"));
        };
    }

    private static Func<TapePlayContext, TapeCommandResult> ParseNote(TapeParseContext context)
    {
        var operand = context.Reader.Read();
        if (operand.Kind != TapeTokenKind.String)
            context.Report(new("NOTE001", TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Parsing,
                "Note expects one string.", operand.Span));
        return playback => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(playback.SequenceOptions)
            .SlowType(operand.Value));
    }
}
