<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Tree(t => [
        t.Item("Documents", docs => [
            docs.Item("Resume.pdf").Icon("📄"),
            docs.Item("Cover Letter.docx").Icon("📄")
        ]).Icon("📁").Expanded(),
        t.Item("Pictures", pics => [
            pics.Item("Vacation").Icon("📁"),
            pics.Item("Family").Icon("📁")
        ]).Icon("📸"),
        t.Item("README.md").Icon("📄")
    ]))
    .Build();

await terminal.RunAsync();`

const lazyLoadCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Tree(t => [
        t.Item("Server 1").Icon("🖥️")
            .OnExpanding(async _ => {
                await Task.Delay(1000); // Simulate network call
                return [
                    new TreeContext().Item("Database").Icon("🗃️"),
                    new TreeContext().Item("Cache").Icon("💾")
                ];
            }),
        t.Item("Server 2").Icon("🖥️")
            .OnExpanding(async _ => {
                await Task.Delay(500);
                return [
                    new TreeContext().Item("API Gateway").Icon("🌐")
                ];
            })
    ]))
    .Build();

await terminal.RunAsync();`

const multiSelectCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Tree(t => [
            t.Item("Frontend", fe => [
                fe.Item("React"),
                fe.Item("Vue"),
                fe.Item("Angular")
            ]).Expanded(),
            t.Item("Backend", be => [
                be.Item("Node.js"),
                be.Item("Python"),
                be.Item("Go")
            ]).Expanded()
        ])
        .MultiSelect()
        .OnSelectionChanged(e => {
            var selected = e.SelectedItems.Select(i => i.Label);
            Console.WriteLine($"Selected: {string.Join(", ", selected)}");
        })
    ]))
    .Build();

await terminal.RunAsync();`

const dataBoundCode = `using Hex1b;
using Hex1b.Widgets;

record FileNode(string Name, bool IsFolder, FileNode[] Children);

var fileSystem = new[] {
    new FileNode("src", true, [
        new FileNode("Program.cs", false, []),
        new FileNode("Utils.cs", false, [])
    ]),
    new FileNode("README.md", false, [])
};

var activatedItem = "(none)";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Activated: {activatedItem}"),
        v.Text(""),
        v.Tree(
            fileSystem,
            labelSelector: f => f.Name,
            childrenSelector: f => f.Children,
            iconSelector: f => f.IsFolder ? "📁" : "📄"
        )
        .OnItemActivated(e => {
            var file = e.Item.GetData<FileNode>();
            activatedItem = file.Name;
        })
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# Tree

The Tree widget displays hierarchical data with expand/collapse, keyboard navigation, and optional multi-selection with cascade behavior.

## Basic Usage

Create a tree using the `Tree()` extension method with a builder callback. Use `t.Item()` to create items, optionally with nested children.

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="tree-basic" exampleTitle="Tree Widget - Basic Usage" />

**Key features:**
- Use `Icon()` to add an emoji or character prefix
- Use `Expanded()` to show children by default
- Use `Children()` to nest static child items
- Navigate with arrow keys, Enter to activate, Space to expand/collapse

## Lazy Loading

For dynamic data, use `OnExpanding()` to load children asynchronously when the user expands an item. A spinner automatically displays during loading.

<CodeBlock lang="csharp" :code="lazyLoadCode" command="dotnet run" example="tree-lazy-load" exampleTitle="Tree Widget - Lazy Loading" />

**How it works:**
- `OnExpanding()` is called the first time an item is expanded
- The item shows a spinner instead of the expand indicator while loading
- Children are cached after loading (subsequent expands don't reload)

## Multi-Select with Cascade Selection

Enable multi-select mode to show checkboxes. When enabled, selecting a parent automatically selects all children (cascade selection).

<CodeBlock lang="csharp" :code="multiSelectCode" command="dotnet run" example="tree-multi-select" exampleTitle="Tree Widget - Multi-Select" />

**Selection behavior:**
- Click checkbox or press Space to toggle selection
- Selecting a parent selects all descendants
- Deselecting a child shows indeterminate state (`[-]`) on the parent
- `OnSelectionChanged` fires with the list of selected items

## Data Binding

Bind a tree to a data source using the generic `Tree<T>()` overload. Use `Data<T>()` to associate typed data with items, and `GetData<T>()` to retrieve it in event handlers.

<CodeBlock lang="csharp" :code="dataBoundCode" command="dotnet run" example="tree-data-binding" exampleTitle="Tree Widget - Data Binding" />

**Type-safe data access:**
- `Data<T>(value)` — Associate data when creating the item
- `GetData<T>()` — Retrieve data in handlers (throws if type mismatch)
- `TryGetData<T>(out value)` — Non-throwing alternative
- `GetDataOrDefault<T>()` — Returns default if not set

## Keyboard Navigation

| Key | Action |
|-----|--------|
| ↑/↓ | Move focus between items |
| ←/→ | Collapse/Expand focused item |
| Enter | Activate the focused item |
| Space | Toggle expand (or selection in multi-select mode) |
| Tab | Move to next focusable widget |

## API Reference

### TreeWidget

| Method | Description |
|--------|-------------|
| `MultiSelect(bool)` | Enable checkboxes with cascade selection |
| `OnSelectionChanged(handler)` | Called when selection changes |
| `OnItemActivated(handler)` | Called when an item is activated (Enter) |

### TreeItemWidget

| Method | Description |
|--------|-------------|
| `Icon(string)` | Set the icon/emoji prefix |
| `Children(items...)` | Set static child items |
| `Expanded(bool)` | Set initial expanded state |
| `Selected(bool)` | Set initial selection state |
| `Loading(bool)` | Show loading spinner |
| `Data<T>(value)` | Associate typed data |
| `OnExpanding(handler)` | Lazy-load children when expanding |
| `OnExpanded(handler)` | Called after expansion completes |
| `OnCollapsed(handler)` | Called when item is collapsed |
| `OnActivated(handler)` | Called when item is activated |

### TreeItemNode (in event handlers)

| Method | Description |
|--------|-------------|
| `GetData<T>()` | Get typed data (throws if wrong type) |
| `TryGetData<T>(out T)` | Try to get typed data |
| `GetDataOrDefault<T>()` | Get data or default value |

## Related Widgets

- [List](/guide/widgets/list) — Flat scrollable lists without hierarchy
- [Table](/guide/widgets/table) — Tabular data with columns
- [Checkbox](/guide/widgets/checkbox) — Standalone checkbox widget
