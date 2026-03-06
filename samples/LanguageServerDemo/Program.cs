using Hex1b;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.Widgets;
using LanguageServerDemo;

// ── Resolve the workspace directory (shipped alongside the demo) ─────
// When running via `dotnet run --project ...`, the binary is in bin/Debug/net10.0/
// and the workspace is copied alongside it. When running the project directly,
// look relative to the project directory.
var workspacePath = FindWorkspace();
if (workspacePath == null)
    throw new DirectoryNotFoundException(
        "Workspace directory not found. Expected 'workspace/' alongside the project or output binary.");

static string? FindWorkspace()
{
    // 1. Next to the output binary (dotnet run / published)
    var binDir = AppContext.BaseDirectory;
    var candidate = Path.Combine(binDir, "workspace");
    if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);

    // 2. Walk up from binary to find the project directory
    var dir = new DirectoryInfo(binDir);
    while (dir != null)
    {
        candidate = Path.Combine(dir.FullName, "workspace");
        if (Directory.Exists(candidate) &&
            File.Exists(Path.Combine(dir.FullName, "LanguageServerDemo.csproj")))
            return Path.GetFullPath(candidate);
        dir = dir.Parent;
    }

    return null;
}

// ── Static highlighter demos (Syntax, Diagnostics, Hover) ───────────

var sampleCsPath = Path.Combine(workspacePath, "Greeter.cs");
var sampleCsCode = File.ReadAllText(sampleCsPath);
var sampleTsPath = Path.Combine(workspacePath, "TaskManager.ts");
var sampleTsCode = File.ReadAllText(sampleTsPath);

var syntaxDoc = new Hex1bDocument(sampleCsCode);
var syntaxState = new EditorState(syntaxDoc);
var syntaxHighlighter = new CSharpSyntaxHighlighter();

var diagDoc = new Hex1bDocument(sampleCsCode);
var diagState = new EditorState(diagDoc);
var diagSyntaxHighlighter = new CSharpSyntaxHighlighter();
var diagHighlighter = new DiagnosticHighlighter();

var hoverDoc = new Hex1bDocument(sampleCsCode);
var hoverState = new EditorState(hoverDoc);
var hoverSyntax = new CSharpSyntaxHighlighter();
var hoverDiag = new DiagnosticHighlighter();
var hoverInfo = new HoverInfoProvider();

// ── Real language server workspace ───────────────────────────────────
// Uses typescript-language-server (tsserver-backed) for real LSP semantic tokens.
// The C# tab uses the regex-based CSharpSyntaxHighlighter as a static provider.

await using var workspace = new Hex1bDocumentWorkspace(workspacePath);

// Register typescript-language-server (tsserver-backed TypeScript/JS LSP)
workspace.AddLanguageServer("ts-ls", lsp => lsp
    .WithServerCommand("typescript-language-server", "--stdio")
    .WithLanguageId("typescript"));
workspace.MapLanguageServer("*.ts", "ts-ls");
workspace.MapLanguageServer("*.tsx", "ts-ls");
workspace.MapLanguageServer("*.js", "ts-ls");

// Open the TypeScript file — backed by the file system
var tsDoc = await workspace.OpenDocumentAsync("TaskManager.ts");
var tsState = new EditorState(tsDoc);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.TabPanel(tp =>
        [
            tp.Tab("TypeScript (LSP)", t =>
            [
                t.VStack(v =>
                [
                    v.Text("Real TypeScript highlighting via typescript-language-server — TaskManager.ts"),
                    v.Separator(),
                    v.Editor(tsState)
                        .LanguageServer(workspace)
                        .LineNumbers()
                        .Fill()
                ]).Fill()
            ]),
            tp.Tab("C# (Static)", t =>
            [
                t.VStack(v =>
                [
                    v.Text("C# Syntax Highlighting via ITextDecorationProvider (regex-based, no LSP)"),
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
        ]).Fill();
    })
    .Build();

await terminal.RunAsync();
