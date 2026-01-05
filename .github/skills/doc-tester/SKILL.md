---
name: doc-tester
description: Agent for validating Hex1b documentation against actual library behavior. Use when auditing documentation accuracy, testing interactive examples, or identifying discrepancies between documentation and implementation.
---

# Documentation Tester Skill

This skill provides guidelines for AI agents to systematically validate the Hex1b documentation site against the actual behavior of the Hex1b library. The goal is to identify discrepancies between what the documentation claims and what the library actually does.

## ⚠️ CRITICAL: User-Centric Testing Approach

**You are testing the documentation as if you were a new user learning Hex1b.**

### Core Principles

1. **Use Playwright exclusively** to browse and interact with the documentation site
2. **Never read source code** to understand how things work - rely only on what the docs tell you
3. **Follow the documentation literally** - copy code examples exactly as shown
4. **Test interactive demos** by actually using them in the browser
5. **Evaluate teaching effectiveness** - can you learn from this documentation?

### What This Means

❌ **DO NOT:**
- Read `src/Hex1b/*.cs` files to understand widget behavior
- Check implementation details in node or widget source
- Look at test files to understand expected behavior
- Use internal knowledge of the codebase

✅ **DO:**
- Use Playwright MCP tools to navigate the documentation site
- Read documentation content as displayed in the browser
- Copy code examples and run them in test projects
- Interact with live demos using Playwright
- Evaluate if explanations make sense without prior knowledge

### When You Get Stuck

If documentation is insufficient to proceed:

1. **Document the blocker** - What were you trying to do? What information was missing?
2. **Describe the gap** - What would a user need to know to succeed?
3. **Hand off to doc-writer** - Create a task for the doc-writer skill to fix it
4. **Move on** - Continue testing other areas

This is valuable feedback! Gaps in documentation are exactly what we're trying to find.

## Testing Goals

### 1. Teaching Effectiveness

Can someone learn Hex1b from these docs alone?

- Are concepts introduced in a logical order?
- Are prerequisites clearly stated?
- Do examples build on each other progressively?
- Is terminology explained before being used?
- Are common mistakes or gotchas addressed?

### 2. Accuracy & Correctness

Does the documentation match reality?

- Do code examples compile and run?
- Do interactive demos behave as described?
- Are API signatures and parameters correct?
- Do claimed features actually exist?
- Are limitations and caveats documented?

## Environment Setup

### Local Development Stack

The doc-tester uses a **local development environment** for fast iteration:

1. **Aspire** launches the documentation site locally
2. **Playwright** browses and interacts with the site
3. **Local NuGet packages** are built for testing code examples

This allows testing documentation changes before they go live.

### Aspire App Model

The Hex1b workspace uses .NET Aspire to orchestrate local services. The app model in `apphost.cs` defines:

| Resource | Type | Purpose |
|----------|------|---------|
| `website` | CSharpApp | WebSocket backend for interactive demos (`src/Hex1b.Website`) |
| `content` | ViteApp | VitePress documentation site (`src/content`) |

The `content` resource serves the documentation. Use `mcp_aspire_list_resources` to get the current URL.

### Starting the Local Environment

⚠️ **CRITICAL: Terminal Isolation**

Aspire must run in an **isolated background process** to avoid interference with other terminal commands. When running commands in the same terminal session as Aspire, the terminal may send signals that stop Aspire unexpectedly.

**Recommended approach - run Aspire with nohup:**

```bash
# Start Aspire in background (won't be interrupted by other commands)
cd /path/to/hex1b
nohup aspire run > /tmp/aspire.log 2>&1 &

# Wait a few seconds for startup, then check logs
sleep 10 && head -15 /tmp/aspire.log
```

**Alternative - use `isBackground: true`:**

When using the `run_in_terminal` tool, set `isBackground: true` for the Aspire command, then run all subsequent commands in separate terminal invocations.

Use the Aspire MCP tools to get the content URL:

```
# Call the MCP tool
mcp_aspire_list_resources

# Look for the content resource endpoint_urls
# Example output: http://content-hex1b.dev.localhost:1189
```

The URL format is typically `http://content-hex1b.dev.localhost:1189` but **always verify with `mcp_aspire_list_resources`** as ports can vary.

**IMPORTANT**: Use the local content URL for all testing, not https://hex1b.dev

### Building Local Packages

To test code examples accurately, build and use local Hex1b packages:

```bash
# Run the build-local-package script in this skill directory
./.github/skills/doc-tester/build-local-package.sh
```

This script:
1. Packs `src/Hex1b` into a local NuGet package
2. Creates a temporary NuGet feed at `.doc-tester-packages/`
3. Generates a `NuGet.config` for test projects

See [Local Package Testing](#local-package-testing) for detailed workflow.

## Purpose

The doc-tester agent produces actionable feedback that can:
1. Trigger the **doc-writer** skill to correct documentation inaccuracies
2. Identify product design issues that need to be addressed in code
3. Surface missing documentation for existing features
4. Find dead or broken interactive examples

## Scope

### What to Test

| Area | Description |
|------|-------------|
| **Conceptual accuracy** | Do explanations match actual implementation? |
| **Code examples** | Do code samples compile and run as described? |
| **Interactive demos** | Do live terminal demos behave as documented? |
| **API references** | Are parameter names, types, and behaviors accurate? |
| **Navigation & links** | Do internal links work? Are pages accessible? |
| **Feature coverage** | Are all significant features documented? |

### Documentation Structure

The documentation site (served by Aspire's `content` resource) includes:

```
/                           # Landing page with feature overview
/guide/getting-started      # Getting started tutorial
/guide/first-app           # Quick start guide
/guide/widgets-and-nodes   # Core architecture concepts
/guide/layout              # Layout system
/guide/input               # Input handling
/guide/testing             # Testing guide
/guide/theming             # Theming system
/guide/widgets/            # Per-widget documentation
    text
    button
    textbox
    list
    stacks
    containers
    navigator
    ...
/deep-dives/               # Advanced topics
    reconciliation
    ...
/api/                      # API reference
/gallery                   # Examples showcase
```

## Testing Workflow

**All testing uses Playwright MCP tools to interact with the documentation site.**

### Phase 0: Environment Preparation

1. **Start Aspire**: Run `aspire run` from the repository root
2. **Get content URL**: Use `mcp_aspire_list_resources` to find the `content` endpoint
3. **Build local packages**: Run `.github/skills/doc-tester/build-local-package.sh`
4. **Note the package version**: The script outputs the version (e.g., `1.0.0-local.20260105120000`)

### Phase 1: Navigate and Read Documentation (Playwright)

Use Playwright MCP tools to browse the documentation:

```
# Take an accessibility snapshot to read page content
mcp_playwright_browser_snapshot

# Navigate to a page
mcp_playwright_browser_click with element="Getting Started link"

# Read interactive demos
mcp_playwright_browser_snapshot
```

For each documentation page:

1. **Navigate to the page** using Playwright click actions
2. **Take a snapshot** to read the page content
3. **Evaluate the content** - Is it clear? Complete? Accurate?
4. **Note any confusion** - What would a new user struggle with?

### Phase 2: Test Code Examples

For each code example shown in the documentation:

1. **Copy the code exactly** as shown in the browser (from snapshot)
2. **Create a test project** in the workspace directory
3. **Build the code** - does it compile without errors?
4. **Run and observe** using TerminalMcp - does it match what the docs describe?

#### Step 1: Create Test Project in Workspace

Use the `.doc-tester-workspace/` directory in the repository root (this is gitignored):

```bash
# Create test project in the workspace (not /tmp!)
cd /path/to/hex1b
mkdir -p .doc-tester-workspace
cd .doc-tester-workspace
dotnet new console -n DocTest --force
cp ../.doc-tester-packages/NuGet.config DocTest/
cd DocTest
dotnet add package Hex1b --version 1.0.0-local.XXXXXX

# Paste the code example into Program.cs and build
dotnet build
```

#### Step 2: Run with TerminalMcp Interactive Shell

⚠️ **CRITICAL: Use an interactive shell, not direct command execution**

After building successfully, use TerminalMcp with an interactive shell to run and validate:

```
# 1. Activate terminal tools
activate_terminal_session_control_tools
activate_terminal_interaction_tools

# 2. Start an interactive bash shell (use start_pwsh_terminal on Windows)
mcp_terminal-mcp_start_bash_terminal workingDirectory="/path/to/hex1b"

# 3. Wait for shell prompt
mcp_terminal-mcp_wait_for_terminal_text text="$" timeoutSeconds=5

# 4. Navigate to the test project
mcp_terminal-mcp_send_terminal_input text="cd .doc-tester-workspace/DocTest"
mcp_terminal-mcp_send_terminal_key key="Enter"
mcp_terminal-mcp_wait_for_terminal_text text="$" timeoutSeconds=5

# 5. Run the TUI application
mcp_terminal-mcp_send_terminal_input text="dotnet run"
mcp_terminal-mcp_send_terminal_key key="Enter"

# 6. Wait for the TUI to render
mcp_terminal-mcp_wait_for_terminal_text text="Expected UI text" timeoutSeconds=15

# 7. Capture and verify the visual output (ALWAYS use both)
mcp_terminal-mcp_capture_terminal_text      # For text-based verification
mcp_terminal-mcp_capture_terminal_screenshot # For visual analysis (colors, layout, styling)

# 8. Interact with the TUI (type, navigate, etc.)
mcp_terminal-mcp_send_terminal_input text="Hello"
mcp_terminal-mcp_send_terminal_key key="Tab"
mcp_terminal-mcp_send_terminal_key key="Enter"

# 9. Verify the result after interaction (ALWAYS use both)
mcp_terminal-mcp_capture_terminal_text      # Text verification
mcp_terminal-mcp_capture_terminal_screenshot # Visual analysis

# 10. Exit the TUI (Ctrl+C) and clean up
mcp_terminal-mcp_send_terminal_key key="c" modifiers=["Ctrl"]
mcp_terminal-mcp_stop_terminal sessionId="..."
mcp_terminal-mcp_remove_session sessionId="..."
```

**Why TerminalMcp?** Standard `dotnet run` cannot capture TUI visual output. TerminalMcp provides a virtual terminal that captures the screen buffer, allowing you to verify:
- The UI renders correctly (use `capture_terminal_screenshot` for visual analysis)
- Text appears where expected (use `capture_terminal_text` for verification)
- Interactions (typing, Tab navigation) work as documented
- Focus states and styling change appropriately (visible in screenshots)

⚠️ **CRITICAL**: Always use BOTH `capture_terminal_text` AND `capture_terminal_screenshot` when testing TUI apps. Text capture verifies content, but screenshots reveal visual elements (colors, borders, focus indicators) that text alone cannot assess.

**Why interactive shell?** Starting `dotnet run` directly can fail because the PTY isn't properly initialized. Starting a shell first ensures proper environment and terminal setup.

### Phase 3: Test Interactive Demos (Playwright)

Interactive demos have a "Run in browser" button. Test them:

```
# Click the demo button
mcp_playwright_browser_click with element="Run in browser"

# Wait for terminal to appear
mcp_playwright_browser_wait_for with text="..." 

# Take snapshot to see the demo
mcp_playwright_browser_snapshot

# Interact with the demo
mcp_playwright_browser_type with text="test input"
mcp_playwright_browser_press with key="Tab"
mcp_playwright_browser_press with key="Enter"

# Snapshot again to see results
mcp_playwright_browser_snapshot
```

Verify:
- Does the demo load?
- Does it match the code example shown?
- Do interactions work as described?

### Phase 4: Evaluate Teaching Effectiveness

As you navigate, evaluate:

1. **Concept flow** - Are ideas introduced in a logical order?
2. **Prerequisites** - Is prior knowledge clearly stated?
3. **Completeness** - Can you accomplish tasks with just the docs?
4. **Clarity** - Would a new user understand this?

**When you get stuck:**
- Document what you were trying to do
- Note what information was missing
- This becomes a doc-writer task

## Focus Area Templates

When given a focus area, use these templates to guide testing:

### Widget Documentation Focus

```markdown
## Widget: [WidgetName]

### Page: /guide/widgets/[widget-name]

#### Navigation (Playwright)
- [ ] Page loads successfully
- [ ] All sections visible in snapshot
- [ ] Code examples readable

#### Content Clarity
- [ ] Widget purpose is clear
- [ ] When to use this widget is explained
- [ ] Basic usage example is complete and runnable

#### Code Example Testing
- [ ] Example 1: Compiles and runs as described
- [ ] Example 2: Compiles and runs as described
- [ ] (etc.)

#### Live Demo Testing (Playwright)
- [ ] Demo loads when clicked
- [ ] Initial state matches description
- [ ] Interactions work as documented
- [ ] Demo matches the code example shown

#### Cross-Reference
- [ ] Matches `src/Hex1b/Widgets/[WidgetName]Widget.cs`
- [ ] Matches `src/Hex1b/Nodes/[WidgetName]Node.cs`
- [ ] Extension methods accurate
- [ ] Theme elements documented
```

### Guide Page Focus

```markdown
## Guide: [Page Title]

### Page: /guide/[page-name]

#### Content Verification
- [ ] Conceptual explanations accurate
- [ ] Terminology consistent with codebase
- [ ] Architecture diagrams current
- [ ] Example code compiles

#### Code Examples
For each example:
- [ ] Example [N]: Compiles successfully
- [ ] Example [N]: Runs as described
- [ ] Example [N]: Output matches documentation

#### Links
- [ ] All internal links work
- [ ] External links accessible
- [ ] "Next Steps" links valid
```

### API Reference Focus

```markdown
## API: [Type/Namespace]

### Page: /api/[path]

#### Completeness
- [ ] All public types documented
- [ ] All public members documented
- [ ] Parameter descriptions accurate
- [ ] Return value descriptions accurate

#### Accuracy
- [ ] Type signatures match source
- [ ] Default values documented correctly
- [ ] Exceptions documented
- [ ] Examples work as shown
```

## Output Format

After testing a focus area, produce a structured report:

```markdown
# Documentation Test Report

**Focus Area:** [Description]
**Date:** [ISO Date]
**Tester:** doc-tester agent

## Summary

| Category | Passed | Failed | Warnings |
|----------|--------|--------|----------|
| Content Accuracy | X | Y | Z |
| Code Examples | X | Y | Z |
| Interactive Demos | X | Y | Z |
| Links | X | Y | Z |

## Critical Issues

Issues that make documentation misleading or incorrect.

### Issue 1: [Brief Title]

**Location:** [Page URL and section]
**Type:** [Content/Example/Demo/Link]
**Severity:** Critical

**What the documentation says:**
> [Quote from documentation]

**What actually happens:**
> [Description of actual behavior]

**Evidence:**
[Code snippet, error message, or screenshot description]

**Recommended Action:**
- [ ] Update documentation to match behavior
- [ ] Fix implementation to match documentation
- [ ] Add clarifying note

---

## Warnings

Issues that may confuse readers but aren't strictly incorrect.

### Warning 1: [Brief Title]

**Location:** [Page URL and section]
**Issue:** [Description]
**Suggestion:** [How to improve]

---

## Passed Checks

[List of items that passed validation - brief summary]

## Recommendations

1. **Priority fixes:** [List critical issues to address first]
2. **Documentation gaps:** [Missing documentation to add]
3. **Product issues:** [Implementation bugs discovered]
```

## Local Package Testing

Testing code examples requires running them against the current Hex1b source, not the published NuGet package. This ensures documentation matches the actual behavior of the code in the repository.

### Build Script

The `build-local-package.sh` script in this skill directory handles package creation:

```bash
./.github/skills/doc-tester/build-local-package.sh
```

**What it does:**
1. Cleans any previous local packages
2. Runs `dotnet pack` on `src/Hex1b/Hex1b.csproj`
3. Uses a timestamped version suffix (e.g., `0.35.0-local.20260105120000`)
4. Outputs the package to `.doc-tester-packages/` in the repo root
5. Creates a `NuGet.config` that prioritizes the local feed

**Output:**
```
✓ Built Hex1b 0.35.0-local.20260105120000
✓ Package: /path/to/hex1b/.doc-tester-packages/Hex1b.0.35.0-local.20260105120000.nupkg
✓ NuGet config: /path/to/hex1b/.doc-tester-packages/NuGet.config
```

### Testing Code Examples Workflow

When validating code examples from documentation:

#### Step 1: Create a Test Project in the Workspace

Test projects are created in `.doc-tester-workspace/` within the hex1b repository. This directory is gitignored.

```bash
# Navigate to the hex1b repo root
cd /path/to/hex1b

# Create test project directory (name it descriptively for the test)
mkdir -p .doc-tester-workspace/DocTest
cd .doc-tester-workspace/DocTest

# Create new console project
dotnet new console --force

# Copy the local NuGet.config (contains absolute path to packages)
cp ../../.doc-tester-packages/NuGet.config .
```

#### Step 2: Add Local Package Reference

```bash
# Add the local Hex1b package (use version from build script output)
# The NuGet.config uses an absolute path, so no need to copy the .nupkg file
dotnet add package Hex1b --version 1.0.0-local.20260105120000
```

#### Step 3: Test the Code Example

```bash
# Replace Program.cs with the documentation code sample
cat > Program.cs << 'EOF'
// Paste code example from documentation here
EOF

# Build to verify it compiles
dotnet build
```

#### Step 4: Run as TUI App with TerminalMcp

Hex1b applications are TUI (Terminal User Interface) apps that take over the terminal. Use the TerminalMcp server with an interactive shell:

```
# Start an interactive shell (use start_pwsh_terminal on Windows)
start_bash_terminal workingDirectory="/path/to/hex1b"

# Wait for shell prompt
wait_for_terminal_text text="$" timeoutSeconds=5

# Navigate to test project and run
send_terminal_input text="cd .doc-tester-workspace/DocTest && dotnet run\n"

# Wait for the TUI to render
wait_for_terminal_text text="Expected text" timeoutSeconds=10

# Capture and verify output (ALWAYS use both for TUI apps)
capture_terminal_text      # Text-based verification
capture_terminal_screenshot # Visual analysis (colors, borders, layout)

# Interact with the TUI if needed
send_terminal_key key="Tab"
send_terminal_input text="test"

# Exit the TUI (Ctrl+C or Escape depending on the app)
send_terminal_key key="Escape"

# Clean up the session
remove_session sessionId="..."
```

See the [TerminalMcp Server](#terminalmcp-server) section for full details.

#### Step 5: Compare Behavior

- Does it compile without errors?
- Does it run without exceptions?
- Does the output/behavior match what documentation describes?
- Do keyboard interactions work as documented?

### Automated Example Testing

For testing multiple examples efficiently, use this pattern:

```bash
#!/bin/bash
# test-examples.sh

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
EXAMPLES_DIR="$1"
PACKAGE_VERSION="$2"
NUGET_CONFIG="$REPO_ROOT/.doc-tester-packages/NuGet.config"
WORKSPACE="$REPO_ROOT/.doc-tester-workspace"

# Ensure workspace exists
mkdir -p "$WORKSPACE"

for example in "$EXAMPLES_DIR"/*.cs; do
    name=$(basename "$example" .cs)
    test_dir="$WORKSPACE/test-$name"
    
    echo "Testing: $name"
    
    # Clean up any previous test
    rm -rf "$test_dir"
    mkdir -p "$test_dir"
    cd "$test_dir"
    
    dotnet new console --force > /dev/null
    cp "$NUGET_CONFIG" .
    dotnet add package Hex1b --version "$PACKAGE_VERSION" > /dev/null 2>&1
    cp "$example" Program.cs
    
    if dotnet build > /dev/null 2>&1; then
        echo "  ✓ $name: Compiles"
    else
        echo "  ✗ $name: Build failed"
    fi
done

echo "Test projects preserved in $WORKSPACE for inspection"
```

### NuGet.config Template

The build script creates a NuGet.config with an **absolute path** to the package directory:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="doc-tester-local" value="/absolute/path/to/hex1b/.doc-tester-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="doc-tester-local">
      <package pattern="Hex1b" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

This ensures:
- Hex1b comes from the local package (works when copied to any directory)
- All other dependencies come from nuget.org
- No need to copy the `.nupkg` file to the test project directory

### Cleanup

After testing, clean up:

```bash
# Remove local packages
rm -rf .doc-tester-packages/

# Or run the build script with --clean
./.github/skills/doc-tester/build-local-package.sh --clean
```

## Aspire Integration

The doc-tester uses Aspire to run the documentation site locally. This enables testing against the latest changes before deploying.

### Starting Aspire

```bash
aspire run
```

If prompted about an existing instance, allow it to stop the previous one.

### Finding Resource URLs

Use the Aspire MCP tools to discover service endpoints:

```
# List all resources and their status
list_resources

# Expected output includes:
# - content (ViteApp) → http://localhost:1189
# - website (CSharpApp) → http://localhost:XXXXX
```

The `content` resource URL is the documentation site. Use this URL for all testing instead of https://hex1b.dev.

### Monitoring During Testing

Use Aspire diagnostic tools to debug issues:

| Tool | Purpose |
|------|---------|
| `list_console_logs` | View stdout/stderr from resources |
| `list_structured_logs` | Query structured log entries |
| `list_traces` | View distributed traces |
| `execute_resource_command` | Restart resources if needed |

### Typical Testing Session

```bash
# 1. Start environment
aspire run

# 2. Build local packages (in another terminal)
./.github/skills/doc-tester/build-local-package.sh

# 3. Use MCP tools to get content URL
# list_resources → find content endpoint

# 4. Fetch pages and test
# fetch_webpage with local URLs

# 5. Test code examples
# Create temp projects with local packages

# 6. Check for errors
# list_console_logs for website resource
```

### WebSocket Demo Testing

Interactive demos connect to the `website` resource via WebSocket. When testing demos:

1. Get the `website` resource URL from `list_resources`
2. The demos connect to `/ws/example/{example-id}`
3. Check `list_console_logs` for the `website` resource if demos fail

### Resource Health

Before testing, verify resources are healthy:

```
list_resources
```

Look for:
- ✅ `Running` state for both `content` and `website`
- ✅ HTTP endpoints available
- ⚠️ If `Waiting` or `Failed`, check logs with `list_console_logs`

## TerminalMcp Server

The TerminalMcp server provides tools for running and observing terminal applications. This is essential for testing Hex1b code examples that produce TUI (Terminal User Interface) output, since standard command-line execution cannot capture the visual output of TUI apps.

### When to Use TerminalMcp

Use TerminalMcp when you need to:
- **Run a Hex1b TUI application** and verify its visual output
- **Capture terminal screenshots** (SVG) for documentation verification
- **Interact with running TUI apps** using keyboard input
- **Wait for specific text** to appear on the terminal screen

TerminalMcp solves the limitation that TUI apps take over the terminal and can't be easily observed through standard tooling.

### Supported Shells

TerminalMcp works by starting an **interactive shell** and then sending commands to it like a user would. The supported shells are:

| Shell | OS | Preferred For |
|-------|-----|---------------|
| `bash` | Linux, macOS | Linux and macOS systems |
| `pwsh` (PowerShell) | Windows, Linux, macOS | Windows systems |

**Detecting Available Shells:**

Before starting a terminal session, check which shells are available:

```bash
# On Linux/macOS - check for bash
which bash

# Check for PowerShell (cross-platform)
which pwsh
```

Use `bash` on Linux/macOS and `pwsh` on Windows.

### Available Tools

| Tool | Purpose |
|------|---------|
| `start_bash_terminal` | Start a new bash shell session (Linux/macOS) |
| `start_pwsh_terminal` | Start a new PowerShell session (Windows or cross-platform) |
| `stop_terminal` | Kill the process (session remains for inspection) |
| `remove_session` | Fully dispose and clean up a session |
| `list_terminals` | List all active terminal sessions |
| `send_terminal_input` | Send text input (commands) to the shell |
| `send_terminal_key` | Send special keys (Enter, Tab, arrows, F1-F12) |
| `resize_terminal` | Resize the terminal dimensions |
| `capture_terminal_text` | Get the screen buffer as plain text |
| `capture_terminal_screenshot` | Get the screen as an SVG image |
| `wait_for_terminal_text` | Wait for specific text to appear |

### Interactive Shell Workflow

⚠️ **IMPORTANT: Start a shell, then send commands interactively**

Do NOT try to run `dotnet run` directly as the command. Instead, start an interactive shell and send commands to it like a user would:

```
# 1. Start an interactive bash shell (use start_pwsh_terminal on Windows)
mcp_terminal-mcp_start_bash_terminal workingDirectory="/path/to/hex1b"

# 2. Wait for the shell prompt to appear
mcp_terminal-mcp_wait_for_terminal_text text="$" timeoutSeconds=5

# 3. Send a command by typing it + pressing Enter
mcp_terminal-mcp_send_terminal_input text="cd .doc-tester-workspace/DocTest"
mcp_terminal-mcp_send_terminal_key key="Enter"

# 4. Wait for the command to complete (shell prompt returns)
mcp_terminal-mcp_wait_for_terminal_text text="$" timeoutSeconds=5

# 5. Run the TUI application
mcp_terminal-mcp_send_terminal_input text="dotnet run"
mcp_terminal-mcp_send_terminal_key key="Enter"

# 6. Wait for the TUI to render
mcp_terminal-mcp_wait_for_terminal_text text="TextBox Widget Demo" timeoutSeconds=15

# 7. Capture and verify output
mcp_terminal-mcp_capture_terminal_text

# 8. Interact with the TUI (type, Tab, etc.)
mcp_terminal-mcp_send_terminal_input text="Hello World"
mcp_terminal-mcp_send_terminal_key key="Tab"

# 9. Exit the TUI (Ctrl+C)
mcp_terminal-mcp_send_terminal_key key="c" modifiers=["Ctrl"]

# 10. Clean up
mcp_terminal-mcp_stop_terminal sessionId="..."
mcp_terminal-mcp_remove_session sessionId="..."
```

### Why Interactive Shell?

The dedicated shell tools (`start_bash_terminal`, `start_pwsh_terminal`) ensure:
- Proper environment setup (PATH, etc.)
- Correct working directory handling
- Better PTY initialization for TUI apps
- The ability to run multiple commands in sequence

### Testing Code Examples with TerminalMcp

For Hex1b TUI applications:

1. **Build the test project** first using `run_in_terminal` (standard terminal)
2. **Start an interactive shell** in TerminalMcp
3. **Navigate to the project directory** using `cd` command
4. **Run `dotnet run`** by sending the command + Enter
5. **Wait for the TUI** to render using `wait_for_terminal_text`
6. **Capture output** using BOTH `capture_terminal_text` AND `capture_terminal_screenshot`
7. **Interact** with the TUI using input tools
8. **Exit and clean up** the session

### Comparing Output to Documentation

When documentation shows expected output:

1. **ALWAYS capture both** text AND screenshot for TUI apps
2. Use `capture_terminal_text` for text-based verification
3. Use `capture_terminal_screenshot` for visual analysis (colors, borders, focus states, layout)
4. Compare against documentation claims
5. Report discrepancies as documentation issues

⚠️ **IMPORTANT**: TUI apps have visual elements (colors, borders, styling) that cannot be assessed from plain text alone. Always generate the screenshot to support complete analysis of the rendered output.

### Limitations

- TerminalMcp runs on the local machine (Linux/macOS with PTY support)
- Sessions persist until explicitly removed - always clean up
- For long-running apps, use `stop_terminal` then `remove_session`

## Playwright MCP Tools Reference

These are the primary tools for testing. **Use these instead of reading source files.**

### Navigation & Reading

| Tool | Purpose |
|------|---------|
| `mcp_playwright_browser_snapshot` | Get accessibility tree of current page (read content) |
| `mcp_playwright_browser_click` | Click links, buttons, demos |
| `mcp_playwright_browser_navigate` | Go to a specific URL |
| `mcp_playwright_browser_type` | Type into text fields |
| `mcp_playwright_browser_press` | Press keyboard keys (Tab, Enter, etc.) |

### Reading Page Content

```
# Navigate to the docs
mcp_playwright_browser_navigate url="http://content-hex1b.dev.localhost:1189/guide/widgets/text"

# Read the page content
mcp_playwright_browser_snapshot
```

The snapshot returns an accessibility tree showing all text content, links, buttons, and interactive elements.

### ⚠️ Visual Verification Limitations

**Accessibility snapshots cannot verify visual correctness.** Snapshots show DOM structure and text content, but NOT:
- Colors and styling
- Visual layout and spacing
- How text overflow actually renders (truncation, wrapping)
- Unicode character display width
- Terminal cursor appearance

**For visual verification, use screenshots:**

```
# Take a screenshot to verify visual output
mcp_playwright_browser_take_screenshot

# Or screenshot a specific element
mcp_playwright_browser_take_screenshot element="Terminal demo output" ref="e123"
```

**When to use screenshots:**
- Verifying "Show output" previews match their descriptions
- Checking interactive terminal demos display correctly
- Confirming Unicode characters (emoji, CJK) render with correct width
- Validating color/styling claims in documentation

### Interacting with Demos

```
# Find and click the demo button
mcp_playwright_browser_click element="Run in browser"

# Wait for demo to load
mcp_playwright_browser_wait_for text="some expected text"

# Interact
mcp_playwright_browser_press key="Tab"
mcp_playwright_browser_type text="Hello"
mcp_playwright_browser_press key="Enter"

# See results
mcp_playwright_browser_snapshot
```

### Handling Terminal Demos

The interactive terminal demos use WebSocket connections. When you click "Run in browser":
1. A terminal overlay appears
2. The example code runs in the terminal
3. You can interact with it using keyboard inputs

Use `mcp_playwright_browser_press` to send keyboard input to the terminal.

## Interaction with Other Skills

### Handoff to doc-writer

When documentation issues are found, create actionable items for the doc-writer skill:

```markdown
## Doc-Writer Task: [Issue Title]

**Source:** doc-tester report [date]
**Priority:** [Critical/High/Medium/Low]

**Page URL:** [URL from Playwright navigation]

**Issue:**
[What's wrong or missing - based on what you saw in the browser]

**User Impact:**
[How this affects someone trying to learn from the docs]

**Suggested fix:**
[What information should be added or corrected]
```

### When You Can't Proceed

If documentation is insufficient to complete a task:

```markdown
## Documentation Gap: [Title]

**What I was trying to do:**
[Task or goal]

**Where I got stuck:**
[Page URL and section]

**What information was missing:**
[What the docs should have told me]

**Questions a user would have:**
1. [Question 1]
2. [Question 2]

**Recommendation:**
Add [specific content] to [specific location]
```

**This is valuable feedback!** Gaps that block a user are high-priority fixes.

### Handoff to Product/Engineering

When the docs are correct but the library doesn't work as documented:

```markdown
## Bug Report: [Issue Title]

**Discovered via:** Documentation testing
**Affected documentation:** [URL]

**Expected behavior (per docs):**
[What documentation says should happen]

**Actual behavior:**
[What actually happens when running the code]

**Code example from docs:**
[The exact code you copied from the documentation]

**Recommendation:**
- [ ] Fix implementation to match documented behavior
- [ ] Update documentation if current behavior is intentional
```

## Best Practices

### Do

- ✅ Use Playwright to read ALL documentation content
- ✅ Copy code examples EXACTLY as shown in the browser
- ✅ Test as if you know nothing about the codebase
- ✅ Document when you get stuck - this is valuable feedback
- ✅ Evaluate if explanations would make sense to a newcomer
- ✅ Test interactive demos by actually interacting with them
- ✅ Note terminology that isn't explained
- ✅ Hand off to doc-writer when you find gaps

### Don't

- ❌ Read source code to understand how things work
- ❌ Use internal knowledge of the codebase
- ❌ Assume you know what something means - check if docs explain it
- ❌ Skip testing code examples (they must actually run)
- ❌ Ignore confusing explanations - report them
- ❌ Continue past blockers without documenting them

## Common Issues to Watch For

### Teaching Gaps

- Concepts used before they're explained
- Missing prerequisites or assumptions
- Jumps in complexity without explanation
- Jargon without definitions

### Documentation Drift

- API changes that weren't reflected in docs
- Renamed properties/methods still using old names
- Removed features still documented
- New features not yet documented

### Example Rot

- Code examples using deprecated APIs
- Missing `using` statements
- Incomplete examples that don't compile
- Examples that compile but don't work as described

### Demo Inconsistencies

- Live demos that don't match static code snippets
- WebSocket examples with different behavior than documented
- Missing or broken interactive examples
- Demo state that persists incorrectly

### Conceptual Inaccuracies

- Explanations that don't match implementation
- Incorrect architecture descriptions
- Misleading performance claims
- Wrong default values documented

## Evolving This Skill

This skill will evolve based on testing experience. When you discover:

1. **New testing patterns** - Add them to the workflow
2. **Common failure modes** - Document them in "Common Issues"
3. **Better output formats** - Update the report template
4. **Integration improvements** - Enhance handoff processes

Track skill evolution in git history for this file.
