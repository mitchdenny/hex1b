# Contributing to Hex1b

Thank you for your interest in contributing to Hex1b! This document provides guidelines and information to help you get started.

## 🚀 Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (preview)
- Git
- A code editor (VS Code recommended)
- A terminal emulator with good ANSI escape sequence support

### Setting Up Your Development Environment

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/mitchdenny/hex1b.git
   cd hex1b
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Run the tests**
   ```bash
   dotnet test
   ```

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
├── tests/Hex1b.Tests/      # Unit tests (xUnit)
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

- Tests use xUnit framework
- Test files are in `tests/Hex1b.Tests/`
- Use `IHex1bTerminal` interface for mocking terminal interactions
- Follow the naming convention: `MethodName_Scenario_ExpectedBehavior`

Example:
```csharp
[Fact]
public void HandleInput_EnterKey_TriggersOnClick()
{
    var clicked = false;
    var node = new ButtonNode { Label = "Test", OnClick = () => clicked = true };
    
    var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
    
    Assert.Equal(InputResult.Handled, result);
    Assert.True(clicked);
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
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)

## ❓ Questions?

If you have questions about contributing:
- Open a [Discussion](https://github.com/hex1b/hex1b/discussions)
- Check existing issues for similar topics

Thank you for contributing! 🎉
