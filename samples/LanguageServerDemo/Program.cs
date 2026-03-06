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

// Hover info demo reuses diagnostic code
var hoverDoc = new Hex1bDocument(diagnosticCode);
var hoverState = new EditorState(hoverDoc);
var hoverSyntax = new CSharpSyntaxHighlighter();
var hoverDiag = new DiagnosticHighlighter();
var hoverInfo = new HoverInfoProvider();

// LSP demo — in-process language server
var lspServer = new InProcessLanguageServer();
lspServer.Start();
var lspDoc = new Hex1bDocument(diagnosticCode);
var lspState = new EditorState(lspDoc);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
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
            ]),
            tp.Tab("Hover Info", t =>
            [
                t.VStack(v =>
                [
                    v.Text("Overlay Demo — move cursor onto underlined diagnostics to see hover info"),
                    v.Separator(),
                    v.Editor(hoverState)
                        .Decorations(hoverSyntax)
                        .Decorations(hoverDiag)
                        .Decorations(hoverInfo)
                        .LineNumbers()
                        .Fill()
                ]).Fill()
            ]),
            tp.Tab("Language Server", t =>
            [
                t.VStack(v =>
                [
                    v.Text("LSP Integration — in-process server providing semantic tokens + diagnostics"),
                    v.Separator(),
                    v.Editor(lspState)
                        .LanguageServer(lsp => lsp
                            .WithTransport(lspServer.ClientInput, lspServer.ClientOutput)
                            .WithLanguageId("csharp"))
                        .LineNumbers()
                        .Fill()
                ]).Fill()
            ])
        ]).Fill();
    })
    .Build();

await terminal.RunAsync();
