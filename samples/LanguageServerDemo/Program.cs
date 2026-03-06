using Hex1b;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.Layout;
using Hex1b.Widgets;

// =============================================================================
// Language Server Demo - IDE-like editor with real LSP integration
// =============================================================================

// ── Create a temporary workspace that looks like a real TypeScript project ────
var tempWorkspace = Path.Combine(Path.GetTempPath(), "hex1b-lsp-demo-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tempWorkspace);
Directory.CreateDirectory(Path.Combine(tempWorkspace, "src"));
Directory.CreateDirectory(Path.Combine(tempWorkspace, "src", "models"));
Directory.CreateDirectory(Path.Combine(tempWorkspace, "src", "services"));
Directory.CreateDirectory(Path.Combine(tempWorkspace, "src", "utils"));

// ── Root config files ────────────────────────────────────────────────────────

File.WriteAllText(Path.Combine(tempWorkspace, "package.json"), """
{
  "name": "@hex1b/demo-app",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "tsc",
    "start": "node dist/index.js",
    "lint": "eslint src/"
  },
  "devDependencies": {
    "typescript": "^5.4.0"
  }
}
""");

File.WriteAllText(Path.Combine(tempWorkspace, "tsconfig.json"), """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "resolveJsonModule": true,
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true,
    "outDir": "./dist",
    "rootDir": "./src",
    "noEmit": true
  },
  "include": ["src/**/*.ts"],
  "exclude": ["node_modules", "dist"]
}
""");

// ── src/models/ — Domain types ───────────────────────────────────────────────

File.WriteAllText(Path.Combine(tempWorkspace, "src", "models", "task.ts"), """
/**
 * Core domain model for a task in the system.
 */
export type Priority = "low" | "medium" | "high" | "critical";

export type TaskStatus = "pending" | "in_progress" | "completed" | "cancelled";

export interface Task {
    readonly id: string;
    title: string;
    description: string;
    status: TaskStatus;
    priority: Priority;
    tags: string[];
    assignee?: string;
    createdAt: Date;
    updatedAt: Date;
    completedAt?: Date;
}

export interface CreateTaskInput {
    title: string;
    description?: string;
    priority?: Priority;
    tags?: string[];
    assignee?: string;
}

export interface UpdateTaskInput {
    title?: string;
    description?: string;
    priority?: Priority;
    tags?: string[];
    assignee?: string;
}

export interface TaskFilter {
    status?: TaskStatus | TaskStatus[];
    priority?: Priority | Priority[];
    assignee?: string;
    tags?: string[];
    search?: string;
}

export interface TaskStats {
    total: number;
    byStatus: Record<TaskStatus, number>;
    byPriority: Record<Priority, number>;
    overdue: number;
    completedThisWeek: number;
}
""");

File.WriteAllText(Path.Combine(tempWorkspace, "src", "models", "user.ts"), """
/**
 * User and authentication types.
 */
export interface User {
    readonly id: string;
    email: string;
    name: string;
    role: UserRole;
    avatarUrl?: string;
    createdAt: Date;
    lastLoginAt?: Date;
}

export type UserRole = "admin" | "member" | "viewer";

export interface AuthToken {
    accessToken: string;
    refreshToken: string;
    expiresAt: Date;
}

export interface LoginCredentials {
    email: string;
    password: string;
}

export interface UserPreferences {
    theme: "light" | "dark" | "system";
    language: string;
    notifications: {
        email: boolean;
        push: boolean;
        digest: "none" | "daily" | "weekly";
    };
}
""");

File.WriteAllText(Path.Combine(tempWorkspace, "src", "models", "index.ts"), """
export type { Task, CreateTaskInput, UpdateTaskInput, TaskFilter, TaskStats, Priority, TaskStatus } from "./task.js";
export type { User, UserRole, AuthToken, LoginCredentials, UserPreferences } from "./user.js";
""");

// ── src/services/ — Business logic ──────────────────────────────────────────

File.WriteAllText(Path.Combine(tempWorkspace, "src", "services", "task-service.ts"), """
import type { Task, CreateTaskInput, UpdateTaskInput, TaskFilter, TaskStats, Priority, TaskStatus } from "../models/index.js";
import { generateId, matchesFilter } from "../utils/helpers.js";
import { EventEmitter } from "../utils/events.js";

export type TaskEvent =
    | { type: "created"; task: Task }
    | { type: "updated"; task: Task; changes: Partial<Task> }
    | { type: "deleted"; taskId: string }
    | { type: "statusChanged"; task: Task; from: TaskStatus; to: TaskStatus };

/**
 * Manages the lifecycle of tasks with event-driven notifications.
 */
export class TaskService {
    private tasks = new Map<string, Task>();
    private events = new EventEmitter<TaskEvent>();

    /** Subscribe to task lifecycle events. */
    on(handler: (event: TaskEvent) => void): () => void {
        return this.events.on(handler);
    }

    /** Create a new task with sensible defaults. */
    create(input: CreateTaskInput): Task {
        const now = new Date();
        const task: Task = {
            id: generateId(),
            title: input.title,
            description: input.description ?? "",
            status: "pending",
            priority: input.priority ?? "medium",
            tags: input.tags ?? [],
            assignee: input.assignee,
            createdAt: now,
            updatedAt: now,
        };

        this.tasks.set(task.id, task);
        this.events.emit({ type: "created", task });
        return task;
    }

    /** Update an existing task. Returns the updated task or undefined. */
    update(id: string, input: UpdateTaskInput): Task | undefined {
        const task = this.tasks.get(id);
        if (!task) return undefined;

        const changes: Partial<Task> = {};
        if (input.title !== undefined) { changes.title = input.title; task.title = input.title; }
        if (input.description !== undefined) { changes.description = input.description; task.description = input.description; }
        if (input.priority !== undefined) { changes.priority = input.priority; task.priority = input.priority; }
        if (input.tags !== undefined) { changes.tags = input.tags; task.tags = [...input.tags]; }
        if (input.assignee !== undefined) { changes.assignee = input.assignee; task.assignee = input.assignee; }

        task.updatedAt = new Date();
        this.events.emit({ type: "updated", task, changes });
        return task;
    }

    /** Transition a task to a new status. */
    setStatus(id: string, status: TaskStatus): Task | undefined {
        const task = this.tasks.get(id);
        if (!task || task.status === status) return task;

        const from = task.status;
        task.status = status;
        task.updatedAt = new Date();

        if (status === "completed") {
            task.completedAt = new Date();
        }

        this.events.emit({ type: "statusChanged", task, from, to: status });
        return task;
    }

    /** Delete a task by ID. Returns true if it existed. */
    delete(id: string): boolean {
        const existed = this.tasks.delete(id);
        if (existed) {
            this.events.emit({ type: "deleted", taskId: id });
        }
        return existed;
    }

    /** Get a single task by ID. */
    get(id: string): Task | undefined {
        return this.tasks.get(id);
    }

    /** List tasks with optional filtering. */
    list(filter?: TaskFilter): Task[] {
        const results: Task[] = [];
        for (const task of this.tasks.values()) {
            if (!filter || matchesFilter(task, filter)) {
                results.push(task);
            }
        }
        return results.sort((a, b) => {
            const priorityOrder: Record<Priority, number> = { critical: 0, high: 1, medium: 2, low: 3 };
            return priorityOrder[a.priority] - priorityOrder[b.priority];
        });
    }

    /** Compute aggregate statistics across all tasks. */
    stats(): TaskStats {
        const byStatus: Record<TaskStatus, number> = { pending: 0, in_progress: 0, completed: 0, cancelled: 0 };
        const byPriority: Record<Priority, number> = { low: 0, medium: 0, high: 0, critical: 0 };

        const weekAgo = new Date();
        weekAgo.setDate(weekAgo.getDate() - 7);
        let completedThisWeek = 0;

        for (const task of this.tasks.values()) {
            byStatus[task.status]++;
            byPriority[task.priority]++;

            if (task.status === "completed" && task.completedAt && task.completedAt >= weekAgo) {
                completedThisWeek++;
            }
        }

        return {
            total: this.tasks.size,
            byStatus,
            byPriority,
            overdue: 0,
            completedThisWeek,
        };
    }
}
""");

File.WriteAllText(Path.Combine(tempWorkspace, "src", "services", "auth-service.ts"), """
import type { User, AuthToken, LoginCredentials, UserRole } from "../models/index.js";
import { generateId } from "../utils/helpers.js";

/**
 * Handles user authentication and session management.
 */
export class AuthService {
    private users = new Map<string, User & { passwordHash: string }>();
    private sessions = new Map<string, { userId: string; expiresAt: Date }>();
    private currentUser: User | null = null;

    /** Register a new user account. */
    async register(email: string, password: string, name: string, role: UserRole = "member"): Promise<User> {
        for (const existing of this.users.values()) {
            if (existing.email === email) {
                throw new Error(`User with email ${email} already exists`);
            }
        }

        const user: User & { passwordHash: string } = {
            id: generateId(),
            email,
            name,
            role,
            passwordHash: await this.hashPassword(password),
            createdAt: new Date(),
        };

        this.users.set(user.id, user);
        return this.sanitize(user);
    }

    /** Authenticate with email and password. */
    async login(credentials: LoginCredentials): Promise<AuthToken> {
        const user = this.findByEmail(credentials.email);
        if (!user) {
            throw new Error("Invalid credentials");
        }

        const valid = await this.verifyPassword(credentials.password, user.passwordHash);
        if (!valid) {
            throw new Error("Invalid credentials");
        }

        user.lastLoginAt = new Date();
        this.currentUser = this.sanitize(user);

        return this.createToken(user.id);
    }

    /** Validate a token and return the associated user. */
    validate(token: string): User | null {
        const session = this.sessions.get(token);
        if (!session || session.expiresAt < new Date()) {
            if (session) this.sessions.delete(token);
            return null;
        }

        const user = this.users.get(session.userId);
        return user ? this.sanitize(user) : null;
    }

    /** Revoke a session token. */
    logout(token: string): void {
        this.sessions.delete(token);
        this.currentUser = null;
    }

    /** Get the currently authenticated user. */
    getCurrentUser(): User | null {
        return this.currentUser;
    }

    private findByEmail(email: string) {
        for (const user of this.users.values()) {
            if (user.email === email) return user;
        }
        return undefined;
    }

    private createToken(userId: string): AuthToken {
        const accessToken = generateId() + generateId();
        const refreshToken = generateId() + generateId();
        const expiresAt = new Date();
        expiresAt.setHours(expiresAt.getHours() + 24);

        this.sessions.set(accessToken, { userId, expiresAt });
        return { accessToken, refreshToken, expiresAt };
    }

    private sanitize(user: User & { passwordHash: string }): User {
        const { passwordHash: _, ...safe } = user;
        return safe;
    }

    private async hashPassword(password: string): Promise<string> {
        const encoder = new TextEncoder();
        const data = encoder.encode(password + "salt");
        const hash = await crypto.subtle.digest("SHA-256", data);
        return Array.from(new Uint8Array(hash)).map(b => b.toString(16).padStart(2, "0")).join("");
    }

    private async verifyPassword(password: string, hash: string): Promise<boolean> {
        return await this.hashPassword(password) === hash;
    }
}
""");

// ── src/utils/ — Shared utilities ───────────────────────────────────────────

File.WriteAllText(Path.Combine(tempWorkspace, "src", "utils", "helpers.ts"), """
import type { Task, TaskFilter } from "../models/index.js";

let counter = 0;

/** Generates a unique ID string. */
export function generateId(): string {
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `${timestamp}-${random}-${++counter}`;
}

/** Checks whether a task matches the given filter criteria. */
export function matchesFilter(task: Task, filter: TaskFilter): boolean {
    if (filter.status) {
        const statuses = Array.isArray(filter.status) ? filter.status : [filter.status];
        if (!statuses.includes(task.status)) return false;
    }

    if (filter.priority) {
        const priorities = Array.isArray(filter.priority) ? filter.priority : [filter.priority];
        if (!priorities.includes(task.priority)) return false;
    }

    if (filter.assignee && task.assignee !== filter.assignee) {
        return false;
    }

    if (filter.tags && filter.tags.length > 0) {
        const hasAllTags = filter.tags.every(tag => task.tags.includes(tag));
        if (!hasAllTags) return false;
    }

    if (filter.search) {
        const query = filter.search.toLowerCase();
        const searchable = `${task.title} ${task.description}`.toLowerCase();
        if (!searchable.includes(query)) return false;
    }

    return true;
}

/** Formats a date as a relative time string (e.g. "2 hours ago"). */
export function timeAgo(date: Date): string {
    const seconds = Math.floor((Date.now() - date.getTime()) / 1000);

    const intervals: [number, string][] = [
        [31536000, "year"],
        [2592000, "month"],
        [86400, "day"],
        [3600, "hour"],
        [60, "minute"],
        [1, "second"],
    ];

    for (const [secs, label] of intervals) {
        const count = Math.floor(seconds / secs);
        if (count >= 1) {
            return `${count} ${label}${count > 1 ? "s" : ""} ago`;
        }
    }

    return "just now";
}

/** Truncates a string to the given length, adding "…" if needed. */
export function truncate(text: string, maxLength: number): string {
    if (text.length <= maxLength) return text;
    return text.slice(0, maxLength - 1) + "…";
}
""");

File.WriteAllText(Path.Combine(tempWorkspace, "src", "utils", "events.ts"), """
/**
 * A simple typed event emitter.
 */
export class EventEmitter<T> {
    private handlers: Array<(event: T) => void> = [];

    /** Subscribe to events. Returns an unsubscribe function. */
    on(handler: (event: T) => void): () => void {
        this.handlers.push(handler);
        return () => {
            const idx = this.handlers.indexOf(handler);
            if (idx >= 0) this.handlers.splice(idx, 1);
        };
    }

    /** Emit an event to all subscribers. */
    emit(event: T): void {
        for (const handler of this.handlers) {
            try {
                handler(event);
            } catch (err) {
                console.error("Event handler error:", err);
            }
        }
    }

    /** Remove all subscribers. */
    clear(): void {
        this.handlers = [];
    }

    /** Returns the number of active subscribers. */
    get listenerCount(): number {
        return this.handlers.length;
    }
}
""");

// ── src/index.ts — Application entry point ──────────────────────────────────

File.WriteAllText(Path.Combine(tempWorkspace, "src", "index.ts"), """
import { TaskService } from "./services/task-service.js";
import { AuthService } from "./services/auth-service.js";
import { timeAgo } from "./utils/helpers.js";

/**
 * Demo application entry point.
 * Sets up services and runs through a sample workflow.
 */
async function main(): Promise<void> {
    const auth = new AuthService();
    const tasks = new TaskService();

    // Subscribe to task events
    tasks.on((event) => {
        switch (event.type) {
            case "created":
                console.log(`[Task Created] ${event.task.title} (${event.task.priority})`);
                break;
            case "statusChanged":
                console.log(`[Status] ${event.task.title}: ${event.from} → ${event.to}`);
                break;
            case "deleted":
                console.log(`[Deleted] Task ${event.taskId}`);
                break;
        }
    });

    // Register and login
    const user = await auth.register("alice@example.com", "secret123", "Alice");
    const token = await auth.login({ email: "alice@example.com", password: "secret123" });
    console.log(`Logged in as ${user.name}, token expires ${timeAgo(token.expiresAt)}`);

    // Create some tasks
    const t1 = tasks.create({ title: "Set up CI pipeline", priority: "high", tags: ["devops"] });
    const t2 = tasks.create({ title: "Write API documentation", priority: "medium", assignee: user.id });
    const t3 = tasks.create({ title: "Fix login redirect bug", priority: "critical", tags: ["bug", "auth"] });
    tasks.create({ title: "Update dependencies", priority: "low" });
    tasks.create({ title: "Add dark mode support", priority: "medium", tags: ["ui"] });

    // Work on tasks
    tasks.setStatus(t1.id, "in_progress");
    tasks.setStatus(t3.id, "in_progress");
    tasks.setStatus(t3.id, "completed");

    tasks.update(t2.id, { tags: ["docs", "api"], description: "Write OpenAPI spec and usage examples" });

    // Query
    const pending = tasks.list({ status: ["pending", "in_progress"] });
    console.log(`\\nPending tasks (${pending.length}):`);
    for (const task of pending) {
        console.log(`  [${task.priority}] ${task.title} — ${task.status}`);
    }

    // Stats
    const stats = tasks.stats();
    console.log(`\\nStats: ${stats.total} total, ${stats.byStatus.completed} completed, ${stats.completedThisWeek} this week`);

    // Validate session
    const validUser = auth.validate(token.accessToken);
    console.log(`\\nSession valid: ${validUser?.name ?? "expired"}`);
}

main().catch(console.error);
""");

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

// Build file tree from workspace — walk directories recursively
BuildFileTree(tempWorkspace, "", ideState.Files);

static void BuildFileTree(string rootPath, string relativePath, List<WorkspaceFile> files)
{
    var dirPath = string.IsNullOrEmpty(relativePath)
        ? rootPath
        : Path.Combine(rootPath, relativePath);

    // Add files in this directory
    foreach (var file in Directory.GetFiles(dirPath).OrderBy(f => f))
    {
        var name = Path.GetFileName(file);
        var relFile = string.IsNullOrEmpty(relativePath) ? name : Path.Combine(relativePath, name);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var icon = ext switch
        {
            ".ts" => "🟦",
            ".js" => "🟨",
            ".json" => "⚙️",
            _ => "📄",
        };
        files.Add(new WorkspaceFile(name, relFile, icon, ext));
    }

    // Recurse into subdirectories (skip node_modules, dist, etc.)
    foreach (var dir in Directory.GetDirectories(dirPath).OrderBy(d => d))
    {
        var dirName = Path.GetFileName(dir);
        if (dirName is "node_modules" or "dist" or ".git") continue;

        var relDir = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);
        BuildFileTree(rootPath, relDir, files);
    }
}

// ── Embedded terminal (bash in workspace directory) ──────────────────────────
using var terminalCts = new CancellationTokenSource();
Hex1bApp? displayApp = null;

var embeddedTerminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess(opts =>
    {
        opts.FileName = "bash";
        opts.Arguments = ["--norc", "--noprofile"];
        opts.WorkingDirectory = tempWorkspace;
    })
    .WithTerminalWidget(out var shellHandle)
    .Build();

shellHandle.WindowTitleChanged += _ => displayApp?.Invalidate();

_ = Task.Run(async () =>
{
    try { await embeddedTerminal.RunAsync(terminalCts.Token); }
    catch (OperationCanceledException) { }
});

// ── Build the terminal UI ────────────────────────────────────────────────────
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        displayApp = app;
        return ctx =>
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
        // MAIN CONTENT — VSplitter with editor area on top, terminal on bottom
        // ═══════════════════════════════════════════════════════════════════
        main.VSplitter(
            // ─── TOP: HSplitter with Explorer + Editor ─────────────────────
            top => [
                top.HSplitter(
                    // ─── LEFT PANE: File Explorer ─────────────────────────────
            left => [
                left.Text(" EXPLORER"),
                left.Separator(),
                left.VScrollPanel(scroll => [
                    scroll.Tree(t =>
                    {
                        // Build tree reflecting the actual directory structure
                        var tree = new Dictionary<string, List<WorkspaceFile>>();
                        var rootFiles = new List<WorkspaceFile>();

                        foreach (var f in ideState.Files)
                        {
                            var dir = Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/');
                            if (string.IsNullOrEmpty(dir))
                            {
                                rootFiles.Add(f);
                            }
                            else
                            {
                                if (!tree.ContainsKey(dir)) tree[dir] = [];
                                tree[dir].Add(f);
                            }
                        }

                        var items = new List<TreeItemWidget>();

                        // Add directories as nested tree items
                        foreach (var dir in tree.Keys.OrderBy(d => d))
                        {
                            items.Add(BuildDirItem(t, dir, tree[dir]));
                        }

                        // Add root-level files
                        foreach (var f in rootFiles)
                        {
                            items.Add(BuildFileItem(t, f));
                        }

                        return items;
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
            ).Fill()
            ],
            // ─── BOTTOM: Embedded Terminal ────────────────────────────────
            bottom => [
                bottom.Text(" TERMINAL"),
                bottom.Separator(),
                bottom.Terminal(shellHandle)
                    .WhenNotRunning(args => bottom.VStack(v => [
                        v.Text($"  Shell exited (code {args.ExitCode ?? 0}). Restart the demo to get a new terminal.")
                    ]))
                    .Fill()
            ],
            topHeight: 20
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
    ]);
    })
    .Build();

await terminal.RunAsync();

// ── Cleanup ──────────────────────────────────────────────────────────────────
terminalCts.Cancel();
await embeddedTerminal.DisposeAsync();
try { Directory.Delete(tempWorkspace, recursive: true); } catch { }

// =============================================================================
// Helper: open a file from the workspace
// =============================================================================
void OpenFile(WorkspaceFile file, bool keepOpen)
{
    // Check if already open
    var existingIdx = ideState.OpenTabs.FindIndex(t => t.RelativePath == file.RelativePath);
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
    var doc = workspace.OpenDocumentAsync(file.RelativePath).GetAwaiter().GetResult();
    var state = new EditorState(doc);
    return new OpenTab(file.Name, file.RelativePath, file.Extension, doc, state, keepOpen);
}

TreeItemWidget BuildDirItem(TreeContext t, string dirPath, List<WorkspaceFile> files)
{
    var dirName = Path.GetFileName(dirPath);
    return t.Item(dirName, items =>
        files.Select(f => BuildFileItem(t, f))
    ).Icon("📁").Expanded();
}

TreeItemWidget BuildFileItem(TreeContext t, WorkspaceFile f)
{
    return t.Item(f.Name)
        .Icon(f.Icon)
        .OnClicked(e =>
        {
            OpenFile(f, keepOpen: false);
            statusMessage = $"Preview: {f.RelativePath}";
        })
        .OnActivated(e =>
        {
            OpenFile(f, keepOpen: true);
            statusMessage = $"Opened: {f.RelativePath}";
        });
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

record WorkspaceFile(string Name, string RelativePath, string Icon, string Extension);

record OpenTab(
    string FileName,
    string RelativePath,
    string Extension,
    Hex1bDocument Document,
    EditorState EditorState,
    bool KeepOpen);
