using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Hex1b.Layout;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// A fluent builder for describing patterns of terminal cells.
/// Patterns can match based on character content, cell attributes, colors,
/// and spatial relationships. The searcher is immutable - all methods
/// return new instances, making patterns safe to reuse and compose.
/// </summary>
public sealed class CellPatternSearcher
{
    private readonly ImmutableList<IPatternStep> _steps;
    private readonly ImmutableStack<string> _activeCaptures;

    /// <summary>
    /// Creates a new empty pattern searcher.
    /// </summary>
    public CellPatternSearcher()
    {
        _steps = ImmutableList<IPatternStep>.Empty;
        _activeCaptures = ImmutableStack<string>.Empty;
    }

    private CellPatternSearcher(ImmutableList<IPatternStep> steps, ImmutableStack<string> activeCaptures)
    {
        _steps = steps;
        _activeCaptures = activeCaptures;
    }

    private CellPatternSearcher AddStep(IPatternStep step) =>
        new(_steps.Add(step), _activeCaptures);

    #region Find (Starting Points)

    /// <summary>
    /// Starts the pattern by finding all cells matching the predicate.
    /// Each matching cell becomes a potential starting point for the pattern.
    /// </summary>
    public CellPatternSearcher Find(Func<CellMatchContext, bool> predicate) =>
        AddStep(new FindPredicateStep(predicate));

    /// <summary>
    /// Starts the pattern by finding all cells containing the specified character.
    /// </summary>
    public CellPatternSearcher Find(char c) =>
        Find(ctx => ctx.Cell.Character == c.ToString());

    /// <summary>
    /// Starts the pattern by finding all matches of a regex pattern.
    /// Uses the existing FindPattern infrastructure for efficiency.
    /// </summary>
    public CellPatternSearcher Find(string regexPattern, RegexOptions options = RegexOptions.None) =>
        AddStep(new FindRegexStep(new Regex(regexPattern, options)));

    /// <summary>
    /// Starts the pattern by finding all matches of a compiled regex.
    /// </summary>
    public CellPatternSearcher Find(Regex regex) =>
        AddStep(new FindRegexStep(regex));

    #endregion

    #region Directional Movement with Predicate

    /// <summary>
    /// Moves one cell to the right and matches if the predicate returns true.
    /// </summary>
    public CellPatternSearcher Right(Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Right, 1, predicate));

    /// <summary>
    /// Moves one cell to the left and matches if the predicate returns true.
    /// </summary>
    public CellPatternSearcher Left(Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Left, 1, predicate));

    /// <summary>
    /// Moves one cell up and matches if the predicate returns true.
    /// </summary>
    public CellPatternSearcher Up(Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Up, 1, predicate));

    /// <summary>
    /// Moves one cell down and matches if the predicate returns true.
    /// </summary>
    public CellPatternSearcher Down(Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Down, 1, predicate));

    #endregion

    #region Directional Movement with Character

    /// <summary>
    /// Moves one cell to the right and matches if it contains the specified character.
    /// </summary>
    public CellPatternSearcher Right(char c) =>
        Right(ctx => ctx.Cell.Character == c.ToString());

    /// <summary>
    /// Moves one cell to the left and matches if it contains the specified character.
    /// </summary>
    public CellPatternSearcher Left(char c) =>
        Left(ctx => ctx.Cell.Character == c.ToString());

    /// <summary>
    /// Moves one cell up and matches if it contains the specified character.
    /// </summary>
    public CellPatternSearcher Up(char c) =>
        Up(ctx => ctx.Cell.Character == c.ToString());

    /// <summary>
    /// Moves one cell down and matches if it contains the specified character.
    /// </summary>
    public CellPatternSearcher Down(char c) =>
        Down(ctx => ctx.Cell.Character == c.ToString());

    #endregion

    #region Directional Movement with Count

    /// <summary>
    /// Moves the specified number of cells to the right.
    /// </summary>
    public CellPatternSearcher Right(int count) =>
        AddStep(new DirectionalStep(Direction.Right, count, null));

    /// <summary>
    /// Moves the specified number of cells to the left.
    /// </summary>
    public CellPatternSearcher Left(int count) =>
        AddStep(new DirectionalStep(Direction.Left, count, null));

    /// <summary>
    /// Moves the specified number of cells up.
    /// </summary>
    public CellPatternSearcher Up(int count) =>
        AddStep(new DirectionalStep(Direction.Up, count, null));

    /// <summary>
    /// Moves the specified number of cells down.
    /// </summary>
    public CellPatternSearcher Down(int count) =>
        AddStep(new DirectionalStep(Direction.Down, count, null));

    #endregion

    #region Directional Movement with Count and Predicate

    /// <summary>
    /// Moves the specified number of cells to the right, all must match the predicate.
    /// </summary>
    public CellPatternSearcher Right(int count, Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Right, count, predicate));

    /// <summary>
    /// Moves the specified number of cells to the left, all must match the predicate.
    /// </summary>
    public CellPatternSearcher Left(int count, Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Left, count, predicate));

    /// <summary>
    /// Moves the specified number of cells up, all must match the predicate.
    /// </summary>
    public CellPatternSearcher Up(int count, Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Up, count, predicate));

    /// <summary>
    /// Moves the specified number of cells down, all must match the predicate.
    /// </summary>
    public CellPatternSearcher Down(int count, Func<CellMatchContext, bool> predicate) =>
        AddStep(new DirectionalStep(Direction.Down, count, predicate));

    #endregion

    #region Text Sequences

    /// <summary>
    /// Matches a sequence of characters moving to the right.
    /// </summary>
    public CellPatternSearcher RightText(string text) =>
        AddStep(new TextSequenceStep(Direction.Right, text));

    /// <summary>
    /// Matches a sequence of characters moving to the left.
    /// </summary>
    public CellPatternSearcher LeftText(string text) =>
        AddStep(new TextSequenceStep(Direction.Left, text));

    #endregion

    #region While (Consume While True)

    /// <summary>
    /// Moves right while the predicate returns true.
    /// Stops when predicate returns false (that cell is not consumed).
    /// Zero matches is valid.
    /// </summary>
    public CellPatternSearcher RightWhile(Func<CellMatchContext, bool> predicate) =>
        AddStep(new WhileStep(Direction.Right, predicate));

    /// <summary>
    /// Moves left while the predicate returns true.
    /// </summary>
    public CellPatternSearcher LeftWhile(Func<CellMatchContext, bool> predicate) =>
        AddStep(new WhileStep(Direction.Left, predicate));

    /// <summary>
    /// Moves up while the predicate returns true.
    /// </summary>
    public CellPatternSearcher UpWhile(Func<CellMatchContext, bool> predicate) =>
        AddStep(new WhileStep(Direction.Up, predicate));

    /// <summary>
    /// Moves down while the predicate returns true.
    /// </summary>
    public CellPatternSearcher DownWhile(Func<CellMatchContext, bool> predicate) =>
        AddStep(new WhileStep(Direction.Down, predicate));

    #endregion

    #region Until (Consume Until True)

    /// <summary>
    /// Moves right until the predicate returns true (inclusive).
    /// The matching cell is included in the result.
    /// Fails if boundary is reached without a match.
    /// </summary>
    public CellPatternSearcher RightUntil(Func<CellMatchContext, bool> predicate) =>
        AddStep(new UntilStep(Direction.Right, predicate));

    /// <summary>
    /// Moves left until the predicate returns true (inclusive).
    /// </summary>
    public CellPatternSearcher LeftUntil(Func<CellMatchContext, bool> predicate) =>
        AddStep(new UntilStep(Direction.Left, predicate));

    /// <summary>
    /// Moves up until the predicate returns true (inclusive).
    /// </summary>
    public CellPatternSearcher UpUntil(Func<CellMatchContext, bool> predicate) =>
        AddStep(new UntilStep(Direction.Up, predicate));

    /// <summary>
    /// Moves down until the predicate returns true (inclusive).
    /// </summary>
    public CellPatternSearcher DownUntil(Func<CellMatchContext, bool> predicate) =>
        AddStep(new UntilStep(Direction.Down, predicate));

    /// <summary>
    /// Moves right until the specified character is found (inclusive).
    /// </summary>
    public CellPatternSearcher RightUntil(char c) =>
        RightUntil(ctx => ctx.Cell.Character == c.ToString());

    /// <summary>
    /// Moves left until the specified character is found (inclusive).
    /// </summary>
    public CellPatternSearcher LeftUntil(char c) =>
        LeftUntil(ctx => ctx.Cell.Character == c.ToString());

    #endregion

    #region Boundary Movement

    /// <summary>
    /// Moves right to the end of the line.
    /// </summary>
    public CellPatternSearcher RightToEnd() =>
        AddStep(new BoundaryStep(Direction.Right));

    /// <summary>
    /// Moves left to the start of the line.
    /// </summary>
    public CellPatternSearcher LeftToStart() =>
        AddStep(new BoundaryStep(Direction.Left));

    /// <summary>
    /// Moves down to the bottom of the region.
    /// </summary>
    public CellPatternSearcher DownToBottom() =>
        AddStep(new BoundaryStep(Direction.Down));

    /// <summary>
    /// Moves up to the top of the region.
    /// </summary>
    public CellPatternSearcher UpToTop() =>
        AddStep(new BoundaryStep(Direction.Up));

    #endregion

    #region Captures

    /// <summary>
    /// Begins a named capture. All cells traversed until EndCapture()
    /// will be tagged with this capture name.
    /// </summary>
    public CellPatternSearcher BeginCapture(string name) =>
        new(_steps.Add(new BeginCaptureStep(name)), _activeCaptures.Push(name));

    /// <summary>
    /// Ends the most recently started capture.
    /// </summary>
    public CellPatternSearcher EndCapture()
    {
        if (_activeCaptures.IsEmpty)
            throw new InvalidOperationException("No capture to end. Call BeginCapture first.");
        
        return new(_steps.Add(new EndCaptureStep()), _activeCaptures.Pop());
    }

    #endregion

    #region Composition

    /// <summary>
    /// Embeds another pattern's steps at this point.
    /// </summary>
    public CellPatternSearcher Then(CellPatternSearcher subPattern) =>
        AddStep(new CompositeStep(subPattern._steps));

    /// <summary>
    /// Builds and embeds a sub-pattern at this point.
    /// The function receives a new searcher and should return the built pattern.
    /// </summary>
    public CellPatternSearcher Then(Func<CellPatternSearcher, CellPatternSearcher> buildPattern)
    {
        var sub = buildPattern(new CellPatternSearcher());
        return Then(sub);
    }

    /// <summary>
    /// Optionally matches a sub-pattern. If it fails, continues from current position.
    /// </summary>
    public CellPatternSearcher ThenOptional(CellPatternSearcher subPattern) =>
        AddStep(new OptionalStep(subPattern._steps));

    /// <summary>
    /// Tries the first pattern, falls back to the second if the first fails.
    /// </summary>
    public CellPatternSearcher ThenEither(CellPatternSearcher first, CellPatternSearcher second) =>
        AddStep(new EitherStep(first._steps, second._steps));

    /// <summary>
    /// Repeats a pattern while it continues to match.
    /// </summary>
    public CellPatternSearcher ThenRepeat(CellPatternSearcher repeatedPattern) =>
        AddStep(new RepeatStep(repeatedPattern._steps, null));

    /// <summary>
    /// Repeats a pattern exactly N times.
    /// </summary>
    public CellPatternSearcher ThenRepeat(int count, CellPatternSearcher repeatedPattern) =>
        AddStep(new RepeatStep(repeatedPattern._steps, count));

    #endregion

    #region Execution

    /// <summary>
    /// Searches for all occurrences of the pattern in the region.
    /// </summary>
    public CellPatternSearchResult Search(IHex1bTerminalRegion region)
    {
        var executor = new PatternExecutor(region, _steps);
        return executor.Execute();
    }

    /// <summary>
    /// Searches for the first occurrence of the pattern in the region.
    /// </summary>
    public CellPatternMatch? SearchFirst(IHex1bTerminalRegion region)
    {
        var executor = new PatternExecutor(region, _steps);
        return executor.ExecuteFirst();
    }

    #endregion

    internal ImmutableList<IPatternStep> Steps => _steps;
}

/// <summary>
/// Direction of movement in the terminal grid.
/// </summary>
public enum Direction
{
    Right,
    Left,
    Up,
    Down
}
