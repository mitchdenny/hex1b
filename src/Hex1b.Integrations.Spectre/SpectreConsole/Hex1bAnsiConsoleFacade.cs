using Spectre.Console;
using Spectre.Console.Rendering;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// Wraps an inner <see cref="IAnsiConsole"/> created from
/// <see cref="AnsiConsole.Create(AnsiConsoleSettings)"/> and substitutes its
/// <see cref="Input"/> with one that drains a Hex1b workload adapter.
/// </summary>
/// <remarks>
/// Spectre.Console's <c>AnsiConsoleFactory</c> always installs a built-in
/// <c>DefaultInput</c> that reads from <see cref="System.Console"/>. To route
/// keystrokes from the Hex1b pipeline instead, the bridge owns the
/// <see cref="IAnsiConsoleInput"/> and forwards every other member to the
/// underlying console.
/// </remarks>
internal sealed class Hex1bAnsiConsoleFacade : IAnsiConsole
{
    private readonly IAnsiConsole _inner;

    public Hex1bAnsiConsoleFacade(IAnsiConsole inner, IAnsiConsoleInput input)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public Profile Profile => _inner.Profile;

    public IAnsiConsoleCursor Cursor => _inner.Cursor;

    public IAnsiConsoleInput Input { get; }

    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;

    public RenderPipeline Pipeline => _inner.Pipeline;

    public void Clear(bool home) => _inner.Clear(home);

    public void Write(IRenderable renderable) => _inner.Write(renderable);

    public void WriteAnsi(Action<AnsiWriter> action) => _inner.WriteAnsi(action);
}
