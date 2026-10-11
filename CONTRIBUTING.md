# Contributing to Hex1b

Thank you for your interest in contributing to Hex1b! This document provides guidelines and information to help you get started.

## 🚀 Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (preview)
- Git
- A code editor (VS Code recommended)
- A terminal emulator with good ANSI escape sequence support
- Native build tools on Unix: `sudo apt install build-essential` on Ubuntu/Debian,
  `apk add build-base` on Alpine Linux (x64/ARM64),
  or `xcode-select --install` on macOS.

### Setting Up Your Development Environment

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/mitchdenny/hex1b.git
   cd hex1b
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore tests/Hex1b.Tests/Hex1b.Tests.csproj
   ```

3. **Build the library**
   ```bash
   dotnet build src/Hex1b/Hex1b.csproj
   ```

4. **Run the tests**
   ```bash
   dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj
   ```

### Running Samples

Run a sample directly; no separate `make` or native-library copy is needed:

```bash
cd samples/KgpCloudDemo
dotnet run
```

On supported Linux and macOS x64/ARM64 hosts, the build compiles the native
interop library as needed and copies it into the sample's output. Native compiler
errors fail the build instead of leaving a sample that crashes at startup.
KgpCloudDemo also needs a terminal that supports kitty graphics.

An explicit `--runtime` selects the matching native library. Cross-publishing
requires that runtime's library in `src/Hex1b/runtimes/<rid>/native/` when the host
cannot compile it; macOS supports building both macOS architectures locally.

Alpine x64 and ARM64 builds select `linux-musl-x64` and `linux-musl-arm64`
automatically, keeping their native libraries separate from the glibc assets.
To build just the native library on Alpine, run `make -C src/Hex1b/native`.
When using a musl cross-compiler on a glibc host, specify both the compiler and libc:
`make -C src/Hex1b/native CC=musl-gcc TARGET_ARCH=x86_64 TARGET_LIBC=musl`.
Changing the output RID alone does not make a glibc binary musl-compatible.

The musl CI matrix builds the native library in architecture-matched x64 and
ARM64 Alpine containers and checks the ELF dependencies and exports. These legs
do not run the native or managed test suites. Both packaging jobs consume the
native artifacts, and the existing package-content checks require both musl
assets. Build locally on Alpine with Docker (use `linux/arm64` for ARM64):

```bash
docker run --rm --platform linux/amd64 \
  -v "$PWD:/repo" -w /repo alpine:3 \
  sh -ec 'apk add --no-cache build-base bash binutils; make -C src/Hex1b/native all check-exports'
```

For an interactive .NET environment where you can run samples manually, see
the [musl sandbox](samples/MuslSandbox/README.md). It is not part of CI.

The glibc Linux and macOS ARM64 CI jobs use a file-based C# app pinned to a
released Hex1b package to launch the repository's KgpCloudDemo in a PTY with
`dotnet run`. The smoke test
waits for a snapshot containing kitty graphics placements, then checks the exit
code after the demo reaches its frame limit:

```bash
dotnet run .github/scripts/test-native-build.cs
```

### Aspire and Web Samples on Ubuntu

Aspire-hosted web samples also need Node.js/npm and tools for trusting the HTTPS
development certificate:

```bash
sudo apt update
sudo apt install nodejs npm libnss3-tools openssl ca-certificates
export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
```

`libnss3-tools` provides `certutil` for Firefox and Chromium certificate stores.
Run `dotnet dev-certs` as your normal user, not with `sudo`. Persist the
`SSL_CERT_DIR` export in your shell profile and restart browsers and Aspire after
changing trust. The web build CI uses Node.js 24.

## 📁 Project Structure

```
hex1b/
├── src/Hex1b/              # Main library (ships to NuGet)
│   ├── Layout/             # Constraint-based layout (Rect, Size, Constraints)
│   ├── Nodes/              # Render nodes - mutable, handle input & rendering
│   ├── Widgets/            # Widget definitions - immutable configuration
│   ├── Theming/            # Theme system and built-in themes
│   ├── Input/              # Keyboard input handling and key bindings
│   ├── Hex1bApp.cs         # Main application entry point
│   ├── Hex1bRenderContext.cs   # Terminal rendering abstraction
│   └── IHex1bTerminal.cs   # Terminal interface for testing
├── samples/                # Example applications
│   └── Cancellation/       # Master-detail contact editor sample
├── tests/Hex1b.Tests/      # Unit tests (MSTest)
└── apphost.cs              # Aspire app host
```

## 🏗️ Architecture Overview

Hex1b uses a **widget/node separation pattern** inspired by React and Flutter:

### Widgets (Immutable)
- Located in `src/Hex1b/Widgets/`
- Describe *what* to render
- Are immutable configuration objects
- Created fresh each render cycle

### Nodes (Mutable)
- Located in `src/Hex1b/Nodes/`
- Represent *how* to render
- Hold mutable state (focus, cursor position, etc.)
- Persist across render cycles
- Handle input and perform actual rendering

### Reconciliation
- `Hex1bApp.Reconcile()` diffs widgets against existing nodes
- Creates new nodes only when types change
- Updates existing nodes when types match
- Minimizes unnecessary state resets

### Render Loop
1. User code builds widget tree
2. Reconciler updates node tree
3. Layout pass measures and arranges nodes
4. Render pass draws to terminal
5. Wait for input event
6. Dispatch input to focused node
7. Repeat

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ButtonNodeTests"
```

### Writing Tests

- Tests use the MSTest framework (`MSTest.Sdk` 4.x with Microsoft.Testing.Platform)
- Test files are in `tests/Hex1b.Tests/`
- Use `IHex1bTerminal` interface for mocking terminal interactions
- Follow the naming convention: `MethodName_Scenario_ExpectedBehavior`

Example:
```csharp
[TestMethod]
public void HandleInput_EnterKey_TriggersClickAction()
{
    var clicked = false;
    var node = new ButtonNode 
    { 
        Label = "Test", 
        ClickAction = _ => { clicked = true; return Task.CompletedTask; } 
    };
    
    var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
    
    Assert.AreEqual(InputResult.Handled, result);
    Assert.IsTrue(clicked);
}
```

## 💡 Development Guidelines

### Code Style

- Use C# 12+ features (primary constructors, collection expressions, etc.)
- Enable nullable reference types (`#nullable enable`)
- Follow .NET naming conventions
- Keep files focused and reasonably sized
- Prefer composition over inheritance

### Adding a New Widget

1. **Create the widget class** in `src/Hex1b/Widgets/`
   ```csharp
   public record MyWidget(string Property) : Hex1bWidget;
   ```

2. **Create the node class** in `src/Hex1b/Nodes/`
   ```csharp
   public class MyNode : Hex1bNode
   {
       public string Property { get; set; } = "";
       
       public override void Measure(Constraints constraints) { /* ... */ }
       public override void Arrange(Rect rect) { /* ... */ }
       public override void Render(Hex1bRenderContext context) { /* ... */ }
   }
   ```

3. **Add reconciliation** in `Hex1bApp.cs`
   ```csharp
   private static MyNode ReconcileMy(MyNode? existingNode, MyWidget widget)
   {
       var node = existingNode ?? new MyNode();
       node.Property = widget.Property;
       return node;
   }
   ```

4. **Add tests** in `tests/Hex1b.Tests/`

5. **Update documentation** if needed

### Commit Messages

Use clear, descriptive commit messages:
- `feat: Add checkbox widget`
- `fix: Correct focus navigation in HStack`
- `docs: Update README with new examples`
- `test: Add layout constraint tests`
- `refactor: Simplify reconciliation logic`

## 🐛 Reporting Issues

When reporting issues, please include:

1. **Description** - What happened vs what you expected
2. **Reproduction steps** - Minimal code to reproduce the issue
3. **Environment** - .NET version, OS, terminal emulator
4. **Screenshots/recordings** - If visual issues, include terminal output

## 📝 Pull Request Process

1. **Create a feature branch**
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Make your changes**
   - Write tests for new functionality
   - Update documentation as needed
   - Ensure all tests pass

3. **Commit your changes**
   ```bash
   git commit -m "feat: Add my feature"
   ```

4. **Push and create PR**
   ```bash
   git push origin feature/my-feature
   ```

5. **PR Guidelines**
   - Provide a clear description of changes
   - Reference any related issues
   - Ensure CI passes
   - Be responsive to review feedback

## 🔬 Running Samples

Samples can be run individually or via Aspire:

```bash
# Run a sample directly
dotnet run --project samples/Cancellation

# Run with Aspire (for multi-project scenarios)
dotnet run --project apphost.cs
```

## 📚 Resources

- [.NET Console APIs](https://learn.microsoft.com/dotnet/api/system.console)
- [ANSI Escape Codes](https://en.wikipedia.org/wiki/ANSI_escape_code)
- [MSTest Documentation](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-intro)
- [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)

## ❓ Questions?

If you have questions about contributing:
- Open a [Discussion](https://github.com/hex1b/hex1b/discussions)
- Check existing issues for similar topics

Thank you for contributing! 🎉
