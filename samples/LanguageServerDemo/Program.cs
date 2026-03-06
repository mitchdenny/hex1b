using Hex1b;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.Layout;
using Hex1b.Widgets;
using LanguageServerDemo;

// =============================================================================
// Language Server Demo - IDE-like editor with real LSP integration
// =============================================================================

// ── Create a temporary workspace with properly configured TS/JS project ──────
var tempWorkspace = Path.Combine(Path.GetTempPath(), "hex1b-lsp-demo-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tempWorkspace);

// Write package.json so typescript-language-server scopes completions properly
File.WriteAllText(Path.Combine(tempWorkspace, "package.json"), """
{
  "name": "hex1b-lsp-demo",
  "version": "1.0.0",
  "private": true,
  "description": "TypeScript workspace for Hex1b language server demo"
}
""");

// Write tsconfig.json
File.WriteAllText(Path.Combine(tempWorkspace, "tsconfig.json"), """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "strict": true,
    "esModuleInterop": true,
    "outDir": "./dist",
    "noEmit": true
  },
  "include": ["*.ts"]
}
""");

// Write sample TypeScript files
File.WriteAllText(Path.Combine(tempWorkspace, "TaskManager.ts"), """
/**
 * A simple task manager demonstrating TypeScript syntax.
 */
interface Task {
    id: number;
    title: string;
    completed: boolean;
    priority: "low" | "medium" | "high";
    tags: string[];
}

class TaskManager {
    private tasks: Map<number, Task> = new Map();
    private nextId = 1;

    /** Creates a new task and returns its ID. */
    add(title: string, priority: Task["priority"] = "medium"): number {
        const id = this.nextId++;
        const task: Task = { id, title, completed: false, priority, tags: [] };
        this.tasks.set(id, task);
        return id;
    }

    /** Marks a task as completed. */
    complete(id: number): boolean {
        const task = this.tasks.get(id);
        if (!task) return false;
        task.completed = true;
        return true;
    }

    /** Gets all pending tasks, optionally filtered by priority. */
    getPending(priority?: Task["priority"]): Task[] {
        const result: Task[] = [];
        for (const task of this.tasks.values()) {
            if (!task.completed) {
                if (!priority || task.priority === priority) {
                    result.push(task);
                }
            }
        }
        return result;
    }

    /** Adds a tag to a task. */
    addTag(id: number, tag: string): void {
        const task = this.tasks.get(id);
        if (task && !task.tags.includes(tag)) {
            task.tags.push(tag);
        }
    }

    /** Returns a summary of tasks by status. */
    summary(): { total: number; completed: number; pending: number } {
        let completed = 0;
        let pending = 0;
        for (const task of this.tasks.values()) {
            if (task.completed) completed++;
            else pending++;
        }
        return { total: this.tasks.size, completed, pending };
    }
}

// Usage
const manager = new TaskManager();
manager.add("Write documentation", "high");
manager.add("Fix bug #42", "medium");
manager.add("Update dependencies", "low");

manager.complete(1);
manager.addTag(2, "bugfix");

const stats = manager.summary();
console.log(`Tasks: ${stats.total} total, ${stats.completed} done, ${stats.pending} pending`);
""");

File.WriteAllText(Path.Combine(tempWorkspace, "HttpClient.ts"), """
/**
 * A typed HTTP client wrapper with interceptors.
 */
type HttpMethod = "GET" | "POST" | "PUT" | "DELETE" | "PATCH";

interface RequestConfig {
    url: string;
    method: HttpMethod;
    headers?: Record<string, string>;
    body?: unknown;
    timeout?: number;
}

interface Response<T> {
    status: number;
    data: T;
    headers: Record<string, string>;
    ok: boolean;
}

type Interceptor = (config: RequestConfig) => RequestConfig | Promise<RequestConfig>;

class HttpClient {
    private baseUrl: string;
    private defaultHeaders: Record<string, string>;
    private interceptors: Interceptor[] = [];

    constructor(baseUrl: string, headers: Record<string, string> = {}) {
        this.baseUrl = baseUrl.replace(/\/$/, "");
        this.defaultHeaders = {
            "Content-Type": "application/json",
            ...headers,
        };
    }

    /** Adds a request interceptor. */
    use(interceptor: Interceptor): void {
        this.interceptors.push(interceptor);
    }

    /** Sends a GET request. */
    async get<T>(path: string, headers?: Record<string, string>): Promise<Response<T>> {
        return this.request<T>({ url: path, method: "GET", headers });
    }

    /** Sends a POST request. */
    async post<T>(path: string, body: unknown, headers?: Record<string, string>): Promise<Response<T>> {
        return this.request<T>({ url: path, method: "POST", body, headers });
    }

    /** Sends a PUT request. */
    async put<T>(path: string, body: unknown): Promise<Response<T>> {
        return this.request<T>({ url: path, method: "PUT", body });
    }

    /** Sends a DELETE request. */
    async delete<T>(path: string): Promise<Response<T>> {
        return this.request<T>({ url: path, method: "DELETE" });
    }

    private async request<T>(config: RequestConfig): Promise<Response<T>> {
        let finalConfig = { ...config, url: `${this.baseUrl}${config.url}` };
        finalConfig.headers = { ...this.defaultHeaders, ...config.headers };

        for (const interceptor of this.interceptors) {
            finalConfig = await interceptor(finalConfig);
        }

        const response = await fetch(finalConfig.url, {
            method: finalConfig.method,
            headers: finalConfig.headers,
            body: finalConfig.body ? JSON.stringify(finalConfig.body) : undefined,
        });

        const data = await response.json() as T;
        return {
            status: response.status,
            data,
            headers: Object.fromEntries(response.headers.entries()),
            ok: response.ok,
        };
    }
}

// Usage
const api = new HttpClient("https://api.example.com", {
    Authorization: "Bearer token123",
});

api.use((config) => {
    console.log(`[${config.method}] ${config.url}`);
    return config;
});
""");

File.WriteAllText(Path.Combine(tempWorkspace, "utils.js"), """
// @ts-check

/**
 * Utility functions for data transformation.
 */

/**
 * Groups an array of items by a key function.
 * @template T
 * @param {T[]} items
 * @param {(item: T) => string} keyFn
 * @returns {Record<string, T[]>}
 */
function groupBy(items, keyFn) {
    const result = {};
    for (const item of items) {
        const key = keyFn(item);
        if (!result[key]) result[key] = [];
        result[key].push(item);
    }
    return result;
}

/**
 * Debounces a function call.
 * @param {Function} fn
 * @param {number} delay
 * @returns {Function}
 */
function debounce(fn, delay) {
    let timer;
    return function (...args) {
        clearTimeout(timer);
        timer = setTimeout(() => fn.apply(this, args), delay);
    };
}

/**
 * Deep clones an object using structured clone.
 * @template T
 * @param {T} obj
 * @returns {T}
 */
function deepClone(obj) {
    return structuredClone(obj);
}

/**
 * Formats a number as a currency string.
 * @param {number} amount
 * @param {string} currency
 * @returns {string}
 */
function formatCurrency(amount, currency = "USD") {
    return new Intl.NumberFormat("en-US", {
        style: "currency",
        currency,
    }).format(amount);
}

module.exports = { groupBy, debounce, deepClone, formatCurrency };
""");

// Also copy the C# sample from the shipped workspace (for static highlighting)
var shippedWorkspace = FindShippedWorkspace();
string csharpSample;
if (shippedWorkspace != null && File.Exists(Path.Combine(shippedWorkspace, "Greeter.cs")))
{
    csharpSample = File.ReadAllText(Path.Combine(shippedWorkspace, "Greeter.cs"));
}
else
{
    csharpSample = """
    using System;
    namespace Demo;

    public class Greeter
    {
        public string Greet(string name) => $"Hello, {name}!";
    }
    """;
}

File.WriteAllText(Path.Combine(tempWorkspace, "Greeter.cs"), csharpSample);

static string? FindShippedWorkspace()
{
    var binDir = AppContext.BaseDirectory;
    var candidate = Path.Combine(binDir, "workspace");
    if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
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

// ── Set up the document workspace ────────────────────────────────────────────
await using var workspace = new Hex1bDocumentWorkspace(tempWorkspace);

// Register typescript-language-server for TS/JS files
workspace.AddLanguageServer("ts-ls", lsp => lsp
    .WithServerCommand("typescript-language-server", "--stdio")
    .WithLanguageId("typescript"));
workspace.MapLanguageServer("*.ts", "ts-ls");
workspace.MapLanguageServer("*.tsx", "ts-ls");
workspace.MapLanguageServer("*.js", "ts-ls");

// ── Application state ────────────────────────────────────────────────────────
var ideState = new IdeState();
var statusMessage = "Ready — Ctrl+Space for completions, '.' to trigger, Esc to dismiss";
var csHighlighter = new CSharpSyntaxHighlighter();

// Discover workspace files and build file tree entries
foreach (var file in Directory.GetFiles(tempWorkspace).OrderBy(f => f))
{
    var name = Path.GetFileName(file);
    var ext = Path.GetExtension(name).ToLowerInvariant();
    var icon = ext switch
    {
        ".ts" => "🟦",
        ".js" => "🟨",
        ".cs" => "🟪",
        ".json" => "⚙️",
        _ => "📄",
    };
    var category = ext switch
    {
        ".ts" or ".tsx" => "TypeScript",
        ".js" or ".jsx" => "JavaScript",
        ".cs" => "C#",
        ".json" => "Config",
        _ => "Other",
    };
    ideState.Files.Add(new WorkspaceFile(name, icon, category, ext));
}

// ── Build the terminal UI ────────────────────────────────────────────────────
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    ctx.VStack(main => [
        // ═══════════════════════════════════════════════════════════════════
        // MENU BAR
        // ═══════════════════════════════════════════════════════════════════
        main.MenuBar(m => [
            m.Menu("File", m => [
                m.MenuItem("Save").OnActivated(e =>
                {
                    if (ideState.ActiveDocument?.Document.IsDirty == true)
                    {
                        _ = ideState.ActiveDocument.Document.SaveAsync();
                        statusMessage = $"Saved: {ideState.ActiveDocument.FileName}";
                    }
                    else
                    {
                        statusMessage = "Nothing to save";
                    }
                }),
                m.MenuItem("Save All").OnActivated(e =>
                {
                    _ = workspace.SaveAllAsync();
                    statusMessage = "Saved all documents";
                }),
                m.Separator(),
                m.MenuItem("Close Tab").OnActivated(e =>
                {
                    if (ideState.OpenTabs.Count > 0)
                    {
                        var name = ideState.ActiveDocument?.FileName ?? "";
                        ideState.CloseTab(ideState.SelectedTabIndex);
                        statusMessage = $"Closed: {name}";
                    }
                }),
                m.MenuItem("Close All Tabs").OnActivated(e =>
                {
                    ideState.CloseAllTabs();
                    statusMessage = "All tabs closed";
                }),
                m.Separator(),
                m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
            ]),
            m.Menu("Edit", m => [
                m.MenuItem("Undo").Disabled(),
                m.MenuItem("Redo").Disabled(),
                m.Separator(),
                m.MenuItem("Find...").Disabled(),
            ]),
            m.Menu("View", m => [
                m.MenuItem("Explorer").OnActivated(e => statusMessage = "Explorer visible"),
                m.MenuItem("Problems").Disabled(),
                m.MenuItem("Output").Disabled(),
            ]),
            m.Menu("Help", m => [
                m.MenuItem("Keyboard Shortcuts").OnActivated(e =>
                    statusMessage = "Ctrl+Space: completions | '.': trigger | Esc: dismiss | Alt+←/→: tabs"),
                m.Separator(),
                m.MenuItem("About").OnActivated(e =>
                    statusMessage = "Language Server Demo v1.0 — Hex1b TUI Library"),
            ])
        ]),

        // ═══════════════════════════════════════════════════════════════════
        // MAIN CONTENT — HSplitter with Explorer + Editor
        // ═══════════════════════════════════════════════════════════════════
        main.HSplitter(
            // ─── LEFT PANE: File Explorer ─────────────────────────────────
            left => [
                left.Text(" EXPLORER"),
                left.Separator(),
                left.VScrollPanel(scroll => [
                    scroll.Tree(t =>
                    {
                        var categories = ideState.Files
                            .GroupBy(f => f.Category)
                            .OrderBy(g => g.Key);

                        return categories.Select(group =>
                            t.Item(group.Key, items =>
                                group.Select(f => t.Item(f.Name)
                                    .Icon(f.Icon)
                                    .OnClicked(e =>
                                    {
                                        OpenFile(f, keepOpen: false);
                                        statusMessage = $"Preview: {f.Name}";
                                    })
                                    .OnActivated(e =>
                                    {
                                        OpenFile(f, keepOpen: true);
                                        statusMessage = $"Opened: {f.Name}";
                                    })
                                )
                            ).Icon("📁").Expanded()
                        );
                    })
                ]).Fill()
            ],
            // ─── RIGHT PANE: Tabbed Editors ───────────────────────────────
            right => ideState.OpenTabs.Count == 0
                ? [
                    right.Center(c => c.VStack(empty => [
                        empty.Text(""),
                        empty.Text("  No files open"),
                        empty.Text(""),
                        empty.Text("  Click a file in the Explorer to preview it."),
                        empty.Text("  Double-click or press Enter to keep it open."),
                        empty.Text(""),
                        empty.Text("  Keyboard shortcuts:"),
                        empty.Text("    Ctrl+Space — Trigger completions"),
                        empty.Text("    '.'        — Trigger member completions"),
                        empty.Text("    Esc        — Dismiss popup"),
                        empty.Text("    Alt+←/→    — Switch tabs"),
                    ])).Fill()
                ]
                : [
                    right.TabPanel(tp =>
                        ideState.OpenTabs.Select((tab, idx) =>
                        {
                            var tabTitle = tab.KeepOpen ? tab.FileName : $"[{tab.FileName}]";
                            var isDirty = tab.Document.IsDirty;
                            if (isDirty) tabTitle = "● " + tabTitle;

                            return tp.Tab(tabTitle, t =>
                            [
                                t.VStack(v =>
                                {
                                    var editor = v.Editor(tab.EditorState)
                                        .LineNumbers()
                                        .Fill();

                                    // Apply LSP for TS/JS files via workspace
                                    if (tab.Extension is ".ts" or ".tsx" or ".js" or ".jsx")
                                    {
                                        editor = editor.LanguageServer(workspace);
                                    }
                                    // Apply static C# highlighting
                                    else if (tab.Extension == ".cs")
                                    {
                                        editor = editor.Decorations(csHighlighter);
                                    }

                                    return [editor];
                                }).Fill()
                            ])
                            .Selected(idx == ideState.SelectedTabIndex)
                            .RightActions(i => [
                                i.Icon("×").OnClick(e =>
                                {
                                    ideState.CloseTab(idx);
                                    statusMessage = $"Closed: {tab.FileName}";
                                })
                            ]);
                        })
                    )
                    .OnSelectionChanged(e =>
                    {
                        ideState.SelectedTabIndex = e.SelectedIndex;
                        var tab = ideState.ActiveDocument;
                        if (tab != null) statusMessage = $"Editing: {tab.FileName}";
                    })
                    .Full()
                    .Selector()
                    .Fill()
                ],
            leftWidth: 25
        ).Fill(),

        // ═══════════════════════════════════════════════════════════════════
        // INFO BAR (status bar)
        // ═══════════════════════════════════════════════════════════════════
        main.InfoBar(s =>
        {
            var items = new List<IInfoBarChild>
            {
                s.Section(statusMessage),
                s.Spacer(),
            };

            if (ideState.ActiveDocument != null)
            {
                var tab = ideState.ActiveDocument;
                var pos = tab.Document.OffsetToPosition(tab.EditorState.Cursor.Position);
                items.Add(s.Section($"Ln {pos.Line}, Col {pos.Column}"));
                items.Add(s.Separator(" | "));

                var lang = tab.Extension switch
                {
                    ".ts" => "TypeScript",
                    ".tsx" => "TypeScript React",
                    ".js" => "JavaScript",
                    ".cs" => "C#",
                    ".json" => "JSON",
                    _ => "Plain Text",
                };
                items.Add(s.Section(lang));
                items.Add(s.Separator(" | "));
                items.Add(s.Section(tab.Document.IsDirty ? "Modified" : "Saved"));
            }

            if (ideState.OpenTabs.Count > 0)
            {
                items.Add(s.Separator(" | "));
                items.Add(s.Section($"Tab {ideState.SelectedTabIndex + 1}/{ideState.OpenTabs.Count}"));
            }

            return items;
        })
    ]))
    .Build();

await terminal.RunAsync();

// ── Cleanup temp workspace ───────────────────────────────────────────────────
try { Directory.Delete(tempWorkspace, recursive: true); } catch { }

// =============================================================================
// Helper: open a file from the workspace
// =============================================================================
void OpenFile(WorkspaceFile file, bool keepOpen)
{
    // Check if already open
    var existingIdx = ideState.OpenTabs.FindIndex(t => t.FileName == file.Name);
    if (existingIdx >= 0)
    {
        if (keepOpen && !ideState.OpenTabs[existingIdx].KeepOpen)
        {
            ideState.OpenTabs[existingIdx] = ideState.OpenTabs[existingIdx] with { KeepOpen = true };
        }
        ideState.SelectedTabIndex = existingIdx;
        return;
    }

    // If preview mode, replace existing preview tab
    if (!keepOpen)
    {
        var previewIdx = ideState.OpenTabs.FindIndex(t => !t.KeepOpen);
        if (previewIdx >= 0)
        {
            var newTab = CreateTab(file, keepOpen: false);
            ideState.OpenTabs[previewIdx] = newTab;
            ideState.SelectedTabIndex = previewIdx;
            return;
        }
    }

    // Add new tab
    var tab = CreateTab(file, keepOpen);
    ideState.OpenTabs.Add(tab);
    ideState.SelectedTabIndex = ideState.OpenTabs.Count - 1;
}

OpenTab CreateTab(WorkspaceFile file, bool keepOpen)
{
    // Open through workspace so LSP can find it
    var doc = workspace.OpenDocumentAsync(file.Name).GetAwaiter().GetResult();
    var state = new EditorState(doc);
    return new OpenTab(file.Name, file.Extension, doc, state, keepOpen);
}

// =============================================================================
// State classes
// =============================================================================
class IdeState
{
    public List<WorkspaceFile> Files { get; } = [];
    public List<OpenTab> OpenTabs { get; } = [];
    public int SelectedTabIndex { get; set; }

    public OpenTab? ActiveDocument =>
        SelectedTabIndex >= 0 && SelectedTabIndex < OpenTabs.Count
            ? OpenTabs[SelectedTabIndex]
            : null;

    public void CloseTab(int index)
    {
        if (index >= 0 && index < OpenTabs.Count)
        {
            OpenTabs.RemoveAt(index);
            if (SelectedTabIndex >= OpenTabs.Count)
                SelectedTabIndex = Math.Max(0, OpenTabs.Count - 1);
        }
    }

    public void CloseAllTabs()
    {
        OpenTabs.Clear();
        SelectedTabIndex = 0;
    }
}

record WorkspaceFile(string Name, string Icon, string Category, string Extension);

record OpenTab(
    string FileName,
    string Extension,
    Hex1bDocument Document,
    EditorState EditorState,
    bool KeepOpen);
