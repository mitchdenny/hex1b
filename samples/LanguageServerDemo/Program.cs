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

// Sample code with diagnostic patterns for underline demo
var diagnosticCode = """
    using System;

    namespace MyApp;

    public class Calculator
    {
        public int Add(int a, int b)
        {
            var unusedResult = a * b; // Warning: unused variable
            return a + b;
        }

        public int Divide(int a, int b)
        {
            // TODO: handle division by zero
            return a / b;
        }

        public void Legacy()
        {
            DeprecatedMethod(); // Hint: deprecated API
        }

        public void Broken()
        {
            var x = undefinedVar + 1; // Error: undefined identifier
        }
    }
    """;

var syntaxDoc = new Hex1bDocument(sampleCode);
var syntaxState = new EditorState(syntaxDoc);
var syntaxHighlighter = new CSharpSyntaxHighlighter();

var diagDoc = new Hex1bDocument(diagnosticCode);
var diagState = new EditorState(diagDoc);
var diagSyntaxHighlighter = new CSharpSyntaxHighlighter();
var diagHighlighter = new DiagnosticHighlighter();

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
                    v.Editor(syntaxState).Decorations(syntaxHighlighter).LineNumbers().Fill()
                ]).Fill()
            ]),
            tp.Tab("Diagnostics", t =>
            [
                t.VStack(v =>
                [
                    v.Text("Diagnostic Underlines — error (curly red), warning (curly yellow), info (dotted blue), hint (dashed gray)"),
                    v.Separator(),
                    v.Editor(diagState)
                        .Decorations(diagSyntaxHighlighter)
                        .Decorations(diagHighlighter)
                        .LineNumbers()
                        .Fill()
                ]).Fill()
            ])
        ]).Fill();
    })
    .Build();

await terminal.RunAsync();
