---
name: doc-writer
description: Guidelines for producing accurate and maintainable documentation for the Hex1b TUI library. Use when writing XML API documentation comments, creating end-user guides, or updating existing documentation.
---

# Documentation Writer Skill

This skill provides guidelines for AI coding agents to help maintainers produce accurate and easy-to-maintain documentation for the Hex1b project. The repository contains two primary types of documentation, each serving different audiences and following different conventions.

## Documentation Types

### 1. XML API Documentation

**Location**: C# source files in `src/Hex1b/`  
**Audience**: Library consumers, API reference generators, IDE intellisense users  
**Format**: XML doc comments (`///`)  
**Build Setting**: `GenerateDocumentationFile` is enabled in `src/Hex1b/Hex1b.csproj`

#### Purpose

XML documentation comments provide:
- Inline help in IDEs (IntelliSense, code completion)
- Generated API reference documentation
- Contract documentation for public APIs
- Usage examples for complex APIs

#### Best Practices for XML Documentation

##### 1. Document All Public APIs

**Required for**:
- Public classes, structs, records, enums
- Public properties and fields
- Public methods and constructors
- Public events and delegates

**Example**:
```csharp
/// <summary>
/// Represents a color that can be used in the terminal.
/// </summary>
public readonly struct Hex1bColor
{
    /// <summary>
    /// Creates a color from RGB values.
    /// </summary>
    /// <param name="r">Red component (0-255)</param>
    /// <param name="g">Green component (0-255)</param>
    /// <param name="b">Blue component (0-255)</param>
    /// <returns>A new Hex1bColor instance with the specified RGB values.</returns>
    public static Hex1bColor FromRgb(byte r, byte g, byte b) => new(r, g, b);
}
```

##### 2. Write Clear, Concise Summaries

- Start with a verb (e.g., "Creates", "Gets", "Sets", "Calculates")
- Keep it to one or two sentences
- Describe **what** it does, not **how** it does it
- Use third person ("Creates a widget" not "Create a widget")

**Good**:
```csharp
/// <summary>
/// Creates a Layout that wraps a single child widget with clipping enabled.
/// </summary>
```

**Bad**:
```csharp
/// <summary>
/// This method will take the child widget and wrap it in a layout.
/// The layout uses the clipMode parameter which defaults to Clip.
/// </summary>
```

##### 3. Document Parameters and Return Values

Always document:
- `<param>` for each parameter
- `<returns>` for non-void methods
- `<exception>` for thrown exceptions (when applicable)
- `<typeparam>` for generic type parameters

**Example**:
```csharp
/// <summary>
/// Arranges child widgets vertically within the given constraints.
/// </summary>
/// <param name="constraints">The size constraints for layout.</param>
/// <param name="children">The child widgets to arrange.</param>
/// <returns>The total size required for all children.</returns>
/// <exception cref="ArgumentNullException">Thrown when children is null.</exception>
public Size ArrangeVertical(Constraints constraints, Hex1bWidget[] children)
```

##### 4. Include Usage Examples for Complex APIs

Use `<example>` and `<code>` tags for key APIs within the Hex1b framework. Examples should be consise yet contain sufficient context to help developers understand how they are used. For examples that use Hex1bApp to create sample applications favor the use of the fluent API since that is the intended usage pattern.

```csharp
/// <summary>
/// The main entry point for building terminal UI applications.
/// </summary>
/// <example>
/// <para>Create a minimal Hex1b application:</para>
/// <code>
/// using Hex1b;
/// 
/// var app = new Hex1bApp(ctx =&gt;
///     ctx.VStack(v => [
///         v.Text("Hello, Hex1b!"),
///         v.Button("Quit", e => e.Context.RequestStop())
///     ])
/// );
/// 
/// await app.RunAsync();
/// </code>
/// </example>
```

**Note**: HTML-encode special characters in code examples:
- `<` becomes `&lt;`
- `>` becomes `&gt;`
- `&` becomes `&amp;`

##### 5. Use Remarks for Additional Context

Use `<remarks>` for:
- Implementation details that affect usage
- Performance considerations
- Related concepts or cross-references

```csharp
/// <summary>
/// The main entry point for building terminal UI applications.
/// </summary>
/// <remarks>
/// Hex1bApp manages the render loop, input handling, focus management, and reconciliation
/// between widgets (immutable declarations) and nodes (mutable render state).
/// 
/// State management is handled via closures - simply capture your state variables
/// in the widget builder callback.
/// </remarks>
public class Hex1bApp : IDisposable, IAsyncDisposable
```

##### 6. Cross-Reference Related APIs

Use `<see>` and `<seealso>` to link to related types:

```csharp
/// <summary>
/// Theme elements for Scroll widgets.
/// </summary>
/// <seealso cref="ScrollWidget"/>
/// <seealso cref="Hex1bTheme"/>
public static class ScrollTheme
```

##### 7. Internal APIs Can Have Lighter Documentation

Internal APIs (marked with `internal`) should still be documented but can be less formal:
- Brief summary is sufficient
- Skip examples unless complex
- Note: The project suppresses CS1591 warnings for missing XML docs via `<NoWarn>$(NoWarn);CS1591</NoWarn>`

##### 8. Maintain Consistency with Existing Documentation

Review existing XML documentation in the codebase to match:
- Tone and style
- Level of detail
- Terminology (e.g., "widget" vs "control", "terminal" vs "console")

### 2. End-User Documentation

**Location**: `src/content/`  
**Audience**: Hex1b library users (developers building TUI applications)  
**Format**: Markdown with VitePress components  
**Build System**: VitePress (static site generator)

#### Documentation Structure

This is the curernt documentation structure. As the documentation structure evolves, this should be updated to assist future agent development efforts.

```
src/content/
├── index.md                    # Landing page
├── api/
│   └── index.md               # API reference overview
├── guide/
│   ├── getting-started.md     # Tutorial walkthrough
│   ├── first-app.md           # Quick start
│   ├── widgets-and-nodes.md   # Core concepts
│   ├── layout.md              # Layout system
│   ├── input.md               # Input handling
│   ├── testing.md             # Testing guide
│   ├── theming.md             # Theming guide
│   └── widgets/               # Per-widget documentation
│       ├── text.md
│       ├── button.md
│       ├── textbox.md
│       ├── list.md
│       ├── stacks.md
│       ├── containers.md
│       └── navigator.md
├── deep-dives/                # Advanced topics
└── gallery.md                 # Examples showcase
```

#### Best Practices for End-User Documentation

##### 1. Use Progressive Disclosure

Structure documentation from simple to complex:
1. **Getting Started**: Simple, working examples
2. **Guide**: Core concepts and common patterns
3. **Deep Dives**: Advanced topics and internals

**Example**: The `getting-started.md` file walks through building a todo app in 5 progressive steps, each adding new concepts.

##### 2. Include Working Code Examples

Every code example should:
- Be complete and runnable (or clearly marked as a snippet)
- Use realistic, meaningful variable names
- Follow the project's coding conventions
- Be tested to ensure accuracy

**Good**:
```csharp
using Hex1b;

var state = new CounterState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text($"Button pressed {state.Count} times"),
        b.Button("Click me!").OnClick(_ => state.Count++)
    ], title: "Counter Demo")
);

await app.RunAsync();

class CounterState
{
    public int Count { get; set; }
}
```

**Bad** (incomplete, won't compile):
```csharp
var button = new Button();
button.Click = () => count++;
```

##### 3. Use VitePress Components

The site uses custom VitePress components for rich content:

- `<TerminalCommand command="..." />` - Show CLI commands
- `<CodeBlock lang="csharp" :code="variable" />` - Code with syntax highlighting
- `<TerminalDemo example="..." title="..." />` - Interactive terminal demos
- `::: tip` / `::: warning` / `::: danger` - Callout boxes

**Example**:
```markdown
::: tip Generating Full API Docs
Full API documentation can be generated from XML doc comments using tools like DocFX or xmldocmd.

```bash
dotnet build /p:GenerateDocumentationFile=true
xmldocmd src/Hex1b/bin/Release/net10.0/Hex1b.dll docs/api
```
:::
```

##### 4. Live Terminal Demos

The documentation site includes a WebSocket-based terminal that can run live code examples. This gives developers an interactive preview of how their code will behave when run locally.

**Location**: `src/Hex1b.Website/Examples/`  
**Component**: `<TerminalDemo example="..." title="..." />`

###### How It Works

1. The Hex1b.Website project hosts WebSocket endpoints that run actual Hex1b applications
2. Each example in `src/Hex1b.Website/Examples/` implements `IGalleryExample`
3. The VitePress documentation uses the `<TerminalDemo>` component to connect to these endpoints
4. Users can interact with the live terminal directly in their browser

###### Code Duplication

Example code in the documentation may be **duplicated** from the main code samples with minor modifications:

- **Why**: The WebSocket terminal environment has slightly different requirements than running locally (e.g., terminal size handling, connection lifecycle)
- **What changes**: Usually minor adjustments to initialization, cleanup, or terminal configuration
- **Goal**: Give developers an accurate preview of runtime behavior, even if the hosted code differs slightly from the documented snippet

**Example structure**:
```
src/Hex1b.Website/Examples/
├── CounterExample.cs      # Live demo version
├── TodoExample.cs         # Live demo version
└── ...
```

The documentation code block shows the "clean" version a developer would write locally, while the `Examples/` folder contains the WebSocket-compatible version.

###### When to Create Live Demos

✅ **Good candidates for live demos**:
- Interactive widgets (buttons, text boxes, lists)
- Layout examples showing responsive behavior
- Input handling demonstrations
- Theming examples

❌ **Not suitable for live demos**:
- Examples using `Hex1bTerminal` for testing (headless/mock terminals)
- Examples that require local file system access
- Performance benchmarks or stress tests
- Examples with external dependencies

###### Registering Examples in Program.cs

**IMPORTANT**: After creating WebSocket example files, you must register them in `src/Hex1b.Website/Program.cs`. Examples will not be available in the live demos until registered.

Add a line for each example in the appropriate section:

```csharp
// Register [Widget name] widget documentation examples
builder.Services.AddSingleton<IGalleryExample, YourNewExample>();
```

Look for existing example registrations grouped by feature area and add your new examples in the corresponding section. If documenting a new widget, create a new comment section for clarity.

###### Keeping Examples in Sync

When updating documentation examples that have live demos:
1. Update the documentation code block first
2. Apply equivalent changes to the `Examples/` implementation
3. **Register the example in Program.cs** (if new)
4. Test the live demo to ensure it still works
5. Note any intentional differences in comments within the `Examples/` file

##### 4. Explain Concepts, Not Just APIs

Don't just describe what methods exist—explain:
- **Why** a feature exists
- **When** to use it
- **How** it fits into the bigger picture

**Example from `widgets-and-nodes.md`**:
```markdown
## Why This Matters

1. **State Preservation**: Focus doesn't jump around when the UI re-renders
2. **Performance**: Only changed parts of the tree get updated
3. **Simplicity**: You describe the UI declaratively; Hex1b figures out the transitions
```

##### 5. Use Visual Aids

Include:
- ASCII diagrams for architecture
- Tables for comparisons
- Trees for hierarchical concepts

**Example**:
```markdown
| Layer | Type | Mutability | Purpose |
|-------|------|------------|---------|
| **Widget** | `record` | Immutable | Describes the desired UI |
| **Node** | `class` | Mutable | Manages state, renders to terminal |
```

##### 6. Maintain a Consistent Voice

- Use second person ("you") when addressing the reader
- Use active voice ("Create a widget" not "A widget is created")
- Be conversational but professional
- Use emojis sparingly (mainly in headings: ✨, 📋, 🚀, etc.)

##### 7. Link Between Related Documentation

Create a documentation graph:
- Link from tutorials to reference docs
- Link from reference docs to tutorials
- Link between related concepts
- Include "Next Steps" sections

**Example from `getting-started.md`**:
```markdown
## Next Steps

- [Widgets & Nodes](/guide/widgets-and-nodes) - Understand the core architecture
- [Layout System](/guide/layout) - Master the constraint-based layout
- [Input Handling](/guide/input) - Learn about keyboard shortcuts and focus
- [Theming](/guide/theming) - Customize the appearance of your app
```

##### 8. Keep Examples Focused

Each example should demonstrate **one concept**:
- Don't mix multiple new concepts in a single example
- Build on previous examples incrementally
- Extract complex examples to separate files in `samples/`

##### 9. Update Documentation When Changing Code

Documentation changes should accompany code changes:
- New widget → New `guide/widgets/*.md` file
- API change → Update relevant guide docs
- Breaking change → Update migration guide (if exists)

##### 10. Test Your Documentation

Before finalizing documentation:
- Copy/paste code examples and verify they compile
- Run code examples to ensure they work as described
- Check that links aren't broken
- Verify VitePress components render correctly

#### Widget Documentation Template

When documenting widgets in `guide/widgets/`, follow this consistent structure to ensure users can quickly find what they need.

**📖 Canonical Example:** See [guide/widgets/text.md](/src/content/guide/widgets/text.md) for a fully-realized example demonstrating all the patterns described below, including:
- External `.cs` snippet files with `?raw` imports
- Live demos with `<CodeBlock>` for full examples
- Static previews with `<StaticTerminalPreview>` for visual variations
- Proper section structure and related widgets links

##### Required Sections

1. **Title** - Widget name as heading
2. **Basic Usage** - Complete, runnable example with `Hex1bApp` setup
3. **Features/Behavior** - Widget-specific capabilities with demos where applicable
4. **Related Widgets** - Links to related documentation

##### Optional Sections

Include these when they add clear value:

- **API Reference** - Include when there are non-obvious parameters, enums, or extension methods to document. For simple widgets with self-explanatory APIs, the XML documentation is sufficient.

##### Live Terminal Demos for Widget Docs

Widget documentation benefits from live terminal demos using the `<CodeBlock>` component. However, choose the appropriate demo type based on the widget's nature.

##### When to Use Live Demos vs Static Previews

| Use Live Demo (`<CodeBlock>`) | Use Static Preview (`<StaticTerminalPreview>`) |
|-------------------------------|-----------------------------------------------|
| Interactive widgets (buttons, text boxes) | Display-only widgets (text, layout) |
| User input demonstration needed | Showing specific visual states |
| Responsive behavior on resize | Code snippet with visual result |
| Focus/navigation demonstrations | Documenting multiple variations |

**Live demos are best when:**
- There is some interactive element on the screen (buttons, inputs, lists)
- Resizing the terminal results in responsive behavior worth demonstrating
- Focus navigation or keyboard handling is a key feature

**Static previews are best when:**
- The widget is purely visual with no interaction
- You're showing multiple variations of the same feature
- The code snippet is a partial example (not full `Hex1bApp` setup)

##### Creating Live Demos

1. **Create WebSocket example files** in `src/Hex1b.Website/Examples/` for each demo
2. **Use the `<CodeBlock>` component** with the `example` attribute linking to the example Id
3. **Define code in `<script setup>`** at the top of the markdown file

**Example structure for widget docs:**

```markdown
<script setup>
const basicCode = \`using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.WidgetMethod("example")
    ])
));

await app.RunAsync();\`
</script>

# WidgetName

Brief description.

## Basic Usage

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="widget-basic" exampleTitle="Widget - Basic Usage" />
```

**Creating the WebSocket example file** (`src/Hex1b.Website/Examples/WidgetBasicExample.cs`):

```csharp
using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class WidgetBasicExample(ILogger<WidgetBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<WidgetBasicExample> _logger = logger;

    public override string Id => "widget-basic";  // Must match example attribute in CodeBlock
    public override string Title => "Widget - Basic Usage";
    public override string Description => "Demonstrates basic widget usage";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating widget basic example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.WidgetMethod("example")
            ]);
        };
    }
}
```

##### Key Principles

**Always use the fluent API**:
- ✅ `ctx.Text("Hello")` or `v.Text("Hello")`
- ❌ `new TextBlockWidget("Hello")` - Do not show direct widget construction

**Basic Usage must include Hex1bApp**:
```csharp
using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Text("Hello, World!")
));

await app.RunAsync();
```

**Show widgets in context** - Demonstrate usage within layout containers:
```csharp
var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Title"),
        v.Text("Description")
    ])
));
```

##### Template Structure

The following template shows the recommended structure. Adapt it based on the widget's complexity:

```markdown
<script setup>
const basicCode = \`using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.WidgetMethod("example")
));

await app.RunAsync();\`
</script>

# WidgetName

Brief description of what the widget does.

## Basic Usage

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="widget-basic" exampleTitle="Widget - Basic Usage" />

## [Feature Section(s)]

Document widget-specific features with focused examples.

Store code snippets as external `.cs` files and import them with `?raw`:

```markdown
<script setup>
import featureSnippet from './snippets/widget-feature.cs?raw'
</script>

<StaticTerminalPreview svgPath="/svg/widget-feature.svg" :code="featureSnippet" />
```

## Related Widgets

- [RelatedWidget](/guide/widgets/related) - Brief description
```

##### Mirror Warning for Code Samples

When a documentation code sample mirrors a WebSocket example, add a warning comment to remind maintainers to keep them in sync:

```markdown
<!-- 
⚠️ MIRROR WARNING: This code sample mirrors the WebSocket example in:
   src/Hex1b.Website/Examples/WidgetBasicExample.cs
   Keep both files in sync when making changes.
-->
```

Place this comment directly before the code block or `<script setup>` section.

#### Static SVG/HTML Previews

For static previews with interactive cell inspection, use the Static Generator to create SVG and HTML files. These offer:

- **Crisp SVG rendering**: Sharp vector graphics at any resolution
- **Interactive HTML inspector**: Hover over cells to see character, color, and attribute details
- **Minimal embed mode**: Clean presentation for documentation iframes

##### Static Generator Template Location

A template console application is provided at:
```
.github/skills/doc-writer/static-generator/
├── StaticGenerator.csproj  # Project file (references Hex1b)
├── Program.cs              # Template with GenerateSnapshot helper
└── README.md               # Detailed usage instructions
```

##### Step-by-Step Workflow for Agents

**IMPORTANT**: Do NOT modify the template files directly. Always copy to a temporary location first.

**Step 1: Create working directory inside workspace**

```bash
mkdir -p .tmp-static-gen
cp .github/skills/doc-writer/static-generator/* .tmp-static-gen/
```

**Step 2: Fix the project reference path**

The template uses a relative path for the original location. Update the project reference in `.tmp-static-gen/StaticGenerator.csproj`:

```bash
# Change the ProjectReference Include path from:
#   Include="../../../../src/Hex1b/Hex1b.csproj"
# To:
#   Include="../src/Hex1b/Hex1b.csproj"
```

**Step 3: Modify Program.cs in the working copy**

Edit `.tmp-static-gen/Program.cs` and replace the contents of `GenerateSnapshots()` with your specific widget:

```csharp
static async Task GenerateSnapshots(string outputDir)
{
    // Example: Generate a text widget with wrapping
    await GenerateSnapshot(outputDir, "text-wrap-demo", "Text Wrapping Demo", 50, 5,
        ctx => ctx.VStack(v => [
            v.Text(
                "This long paragraph demonstrates how text wrapping works in Hex1b. " +
                "When content exceeds the available width, it automatically breaks " +
                "at word boundaries."
            ).Wrap()
        ]));
}
```

**Step 4: Run the generator**

```bash
cd .tmp-static-gen
dotnet run -- output
```

**Step 5: Verify the output**

Check that the files were created:
```bash
ls -la output/
# Should contain: text-wrap-demo.svg
```

**Step 6: Copy to public assets**

```bash
cp output/*.svg ../src/content/public/svg/
```

**Step 7: Clean up**

```bash
cd ..
rm -rf .tmp-static-gen
```

**Step 8: Use in documentation**

Create a snippets folder alongside your markdown and add the code as a `.cs` file:

```
guide/widgets/
├── your-widget.md
└── snippets/
    └── text-wrap-demo.cs
```

Then import and use it in your markdown:

```markdown
<script setup>
import wrapSnippet from './snippets/text-wrap-demo.cs?raw'
</script>

<StaticTerminalPreview svgPath="/svg/text-wrap-demo.svg" :code="wrapSnippet" />
```

##### GenerateSnapshot Function Signature

```csharp
await GenerateSnapshot(
    outputDir,      // Always use "output"
    "filename",     // Name without extension (creates .svg)
    "Description",  // For console output only
    width,          // Terminal columns
    height,         // Terminal rows
    ctx => widget   // Widget builder using RootContext
);
```

##### StaticTerminalPreview Component

The `<StaticTerminalPreview>` component displays code with an expandable "View output" panel. Clicking the chevron icon slides open a panel showing the rendered terminal output.

This component uses Shiki for syntax highlighting with line numbers, matching the styling of the main `<CodeBlock>` component.

**Usage (recommended - external .cs files with `?raw` import):**

Store code snippets as external `.cs` files in a `snippets/` folder alongside your markdown, then import them using Vite's `?raw` suffix:

```
guide/widgets/
├── text.md
└── snippets/
    ├── text-truncate.cs
    ├── text-wrap.cs
    └── text-ellipsis.cs
```

```markdown
<script setup>
import truncateSnippet from './snippets/text-truncate.cs?raw'
import wrapSnippet from './snippets/text-wrap.cs?raw'
import ellipsisSnippet from './snippets/text-ellipsis.cs?raw'
</script>

<StaticTerminalPreview svgPath="/svg/text-truncate.svg" :code="truncateSnippet" />
```

Benefits of external files:
- Real `.cs` files with proper syntax highlighting in your editor
- Easier to maintain and edit
- Avoids VitePress markdown processing issues
- Cleaner markdown files

**Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `svgPath` | string | required | Path to .svg file relative to public folder |
| `code` | string | (from slot) | Code to display (use with `?raw` imports) |
| `language` | string | "csharp" | Language for syntax highlighting |

**Features:**
- Shiki syntax highlighting with line numbers (matching CodeBlock.vue)
- Chevron icon in header to expand/collapse output
- Inline slide-out panel (no overlay or iframe)
- SVG scales to fill available width
- Cell hover tooltips showing character info, colors, and attributes (parsed from SVG DOM)
- Consistent styling with main documentation code blocks

**Note:** The component parses cell data directly from the SVG's DOM structure (using `data-x`, `data-y` attributes and element properties). No separate HTML file is needed.

##### TerminalSvgOptions

| Option | Default | Description |
|--------|---------|-------------|
| `ShowCellGrid` | `false` | Show grid lines between cells |
| `ShowPixelGrid` | `false` | Show pixel-level grid |
| `DefaultBackground` | `#0f0f1a` | Background color |
| `DefaultForeground` | `#e0e0e0` | Text color |
| `FontFamily` | Cascadia Code stack | Monospace font |
| `FontSize` | `14` | Font size in pixels |
| `CellWidth` | `9` | Cell width in pixels |
| `CellHeight` | `18` | Cell height in pixels |

##### Avoiding Cursor in SVG Output

**IMPORTANT**: Unless your code sample specifically demonstrates cursor/mouse behavior, disable the cursor in generated SVGs. A random cursor block appearing in documentation screenshots confuses readers.

The `Hex1bTerminalSnapshot.ToSvg()` method includes cursor position by default. To hide the cursor, cast to `IHex1bTerminalRegion`:

```csharp
// Cast to IHex1bTerminalRegion to use the overload without cursor
var svg = ((IHex1bTerminalRegion)snapshot).ToSvg(svgOptions);
```

The template's `GenerateSnapshot` function already uses this approach by default.

Only include the cursor when documenting cursor-related features (e.g., TextBox focus states, cursor shapes).

##### Common Widget Patterns

```csharp
// Simple text
await GenerateSnapshot(outputDir, "text-simple", "Simple", 40, 3,
    ctx => ctx.Text("Hello, World!"));

// Text with overflow
await GenerateSnapshot(outputDir, "text-ellipsis", "Ellipsis", 50, 3,
    ctx => ctx.Text("Very long text here...").Ellipsis().FixedWidth(30));

// VStack layout
await GenerateSnapshot(outputDir, "layout-vstack", "VStack", 60, 10,
    ctx => ctx.VStack(v => [
        v.Text("Header"),
        v.Text(""),
        v.Text("Body content")
    ]));

// Border with content
await GenerateSnapshot(outputDir, "border-demo", "Border", 40, 6,
    ctx => ctx.Border(b => [
        b.Text("Content inside border")
    ], title: "Title"));
```

## Documentation Workflow

### For New Features

1. **Write XML docs** as you write the code
   - Document public APIs inline with implementation
   - Use `<summary>`, `<param>`, `<returns>`, `<remarks>`
   - Add `<example>` for non-obvious usage

2. **Create or update end-user guides**
   - Add widget documentation to `guide/widgets/`
   - Update relevant tutorial sections
   - Add usage examples to `getting-started.md` if appropriate

### For Bug Fixes

1. **Check if documentation needs updates**
   - Does the bug fix change behavior documented in guides?
   - Do code examples need updating?

2. **Update affected documentation**
   - Fix incorrect examples
   - Clarify ambiguous descriptions
   - Add warnings about common pitfalls

### For Breaking Changes

1. **Update XML docs** to reflect new signatures/behavior
2. **Update all affected guides** with new patterns
3. **Add migration notes** explaining the change
4. **Update or deprecate old examples**

## Quality Checklist

Before considering documentation complete:

### XML Documentation
- [ ] All public types have `<summary>` tags
- [ ] All public methods have parameter and return documentation
- [ ] Complex APIs include usage examples
- [ ] Cross-references use `<see>` and `<seealso>` tags
- [ ] No HTML-encoding errors in code examples

### End-User Documentation
- [ ] Code examples compile and run
- [ ] Examples are complete (or clearly marked as snippets)
- [ ] Markdown formatting is correct
- [ ] VitePress components are used appropriately
- [ ] Internal links work (relative paths are correct)
- [ ] The documentation follows the project's voice and style
- [ ] Related documentation is cross-linked

## Common Pitfalls to Avoid

### XML Documentation

❌ **Don't repeat the member name in the summary**
```csharp
/// <summary>
/// ButtonWidget is a widget for buttons.
/// </summary>
```

✅ **Do describe what it does**
```csharp
/// <summary>
/// An interactive button that responds to Enter, Space, or mouse clicks.
/// </summary>
```

❌ **Don't use implementation details in public API docs**
```csharp
/// <summary>
/// Sets the internal _label field to the specified value.
/// </summary>
```

✅ **Do describe the effect**
```csharp
/// <summary>
/// Sets the text displayed on the button.
/// </summary>
```

### End-User Documentation

❌ **Don't assume prior knowledge**
```markdown
Use reconciliation to update nodes efficiently.
```

✅ **Do explain concepts**
```markdown
When you call `SetState()`, Hex1b diffs the new widget tree against existing nodes
and updates only what changed. This reconciliation process preserves focus and scroll
state while keeping your UI responsive.
```

❌ **Don't use code snippets without context**
```csharp
.OnClick(_ => count++)
```

✅ **Do provide complete, runnable examples**
```csharp
var state = new CounterState();
var app = new Hex1bApp(ctx =>
    ctx.Button("Increment").OnClick(_ => state.Count++)
);
```

## Tools and Resources

### For XML Documentation
- **Visual Studio**: IntelliSense shows XML docs while typing
- **Rider**: Similar XML doc support
- **xmldocmd**: Generate markdown from XML docs (`dotnet tool install -g xmldocmd`)
- **DocFX**: Microsoft's documentation generator

### For End-User Documentation
- **VitePress**: Static site generator (configured in `src/content/.vitepress/`)
- **Markdown Preview**: Any Markdown-capable editor
- **Vale**: Prose linter (optional, for style consistency)

### Documentation References
- [C# XML Documentation Comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [VitePress Documentation](https://vitepress.dev/)
- [Microsoft Writing Style Guide](https://learn.microsoft.com/en-us/style-guide/welcome/)

## Summary

Good documentation:
1. **Serves its audience**: API docs for developers, guides for users
2. **Is accurate**: Code examples compile and run
3. **Is complete**: Covers common scenarios and edge cases
4. **Is consistent**: Follows established patterns and style
5. **Is maintained**: Updated alongside code changes

When in doubt, look at existing documentation in the repository and match its style, tone, and level of detail.
