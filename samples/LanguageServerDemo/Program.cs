using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;
using LanguageServerDemo;

// Sample C# code to display with syntax highlighting
var sampleCode = """
    using System;
    using System.Collections.Generic;

    namespace MyApp;

    // A simple greeter service
    public sealed class Greeter
    {
        private readonly string _name;

        public Greeter(string name)
        {
            _name = name ?? throw new ArgumentException("Name required");
        }

        public string Greet()
        {
            var greeting = $"Hello, {_name}!";
            Console.WriteLine(greeting);
            return greeting;
        }

        public async Task<List<string>> GetHistory(int count)
        {
            var results = new List<string>();
            for (var i = 0; i < count; i++)
            {
                results.Add(Greet());
                await Task.Delay(100);
            }
            return results;
        }
    }
    """;

var syntaxDoc = new Hex1bDocument(sampleCode);
var syntaxState = new EditorState(syntaxDoc);
var syntaxHighlighter = new CSharpSyntaxHighlighter();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.TabPanel(tp =>
        [
            tp.Tab("Syntax Highlighting", t =>
            [
                t.VStack(v =>
                [
                    v.Text("C# Syntax Highlighting via ITextDecorationProvider"),
                    v.Separator(),
                    v.Editor(syntaxState).Decorations(syntaxHighlighter).Fill()
                ]).Fill()
            ])
        ]).Fill();
    })
    .Build();

await terminal.RunAsync();
