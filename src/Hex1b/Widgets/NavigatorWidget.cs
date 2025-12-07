using System.Diagnostics.CodeAnalysis;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a screen/page that can be displayed in a Navigator.
/// </summary>
/// <param name="Id">Unique identifier for this route.</param>
/// <param name="Builder">Function that builds the widget for this route, given the navigator for sub-navigation.</param>
[Experimental("HEX1B001")]
public record NavigatorRoute(string Id, Func<NavigatorState, Hex1bWidget> Builder);

/// <summary>
/// Internal entry that pairs a route with its saved focus state.
/// </summary>
[Experimental("HEX1B001")]
internal class NavigatorStackEntry
{
    public NavigatorRoute Route { get; }
    public int SavedFocusIndex { get; set; } = 0;

    public NavigatorStackEntry(NavigatorRoute route)
    {
        Route = route;
    }
}

/// <summary>
/// State object for Navigator that manages the navigation stack.
/// Use this to push new screens, pop back, or reset to root.
/// </summary>
[Experimental("HEX1B001")]
public class NavigatorState
{
    private readonly Stack<NavigatorStackEntry> _navigationStack = new();
    private readonly NavigatorRoute _rootRoute;

    /// <summary>
    /// Creates a new NavigatorState with the specified root route.
    /// </summary>
    /// <param name="rootRoute">The initial/home route that can be returned to via PopToRoot.</param>
    public NavigatorState(NavigatorRoute rootRoute)
    {
        _rootRoute = rootRoute ?? throw new ArgumentNullException(nameof(rootRoute));
        _navigationStack.Push(new NavigatorStackEntry(rootRoute));
    }

    /// <summary>
    /// Gets the current route at the top of the navigation stack.
    /// </summary>
    public NavigatorRoute CurrentRoute => _navigationStack.Peek().Route;

    /// <summary>
    /// Gets the current stack entry (internal use for focus tracking).
    /// </summary>
    internal NavigatorStackEntry CurrentEntry => _navigationStack.Peek();

    /// <summary>
    /// The entry to save focus to when pushing (set during push, cleared after reconcile).
    /// This is the entry we're navigating AWAY from.
    /// </summary>
    internal NavigatorStackEntry? EntryToSaveFocusTo { get; private set; }

    /// <summary>
    /// The focus index to restore after a pop, or null if not returning from pop.
    /// </summary>
    internal int? PendingFocusRestore { get; private set; }

    /// <summary>
    /// Gets the number of routes in the navigation stack.
    /// </summary>
    public int Depth => _navigationStack.Count;

    /// <summary>
    /// Returns true if there are routes to pop (more than just the root).
    /// </summary>
    public bool CanGoBack => _navigationStack.Count > 1;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event Action? OnNavigated;

    /// <summary>
    /// Clears the pending focus restore after it has been consumed.
    /// </summary>
    internal void ClearPendingFocusRestore()
    {
        PendingFocusRestore = null;
        EntryToSaveFocusTo = null;
    }

    /// <summary>
    /// Pushes a new route onto the navigation stack (drill down).
    /// </summary>
    /// <param name="route">The route to navigate to.</param>
    public void Push(NavigatorRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        // Save reference to entry we're navigating away from
        EntryToSaveFocusTo = _navigationStack.Count > 0 ? _navigationStack.Peek() : null;
        _navigationStack.Push(new NavigatorStackEntry(route));
        PendingFocusRestore = null; // Not a pop
        OnNavigated?.Invoke();
    }

    /// <summary>
    /// Pushes a new route using a simple builder function.
    /// </summary>
    /// <param name="id">Unique identifier for this route.</param>
    /// <param name="builder">Function that builds the widget for this route.</param>
    public void Push(string id, Func<NavigatorState, Hex1bWidget> builder)
    {
        Push(new NavigatorRoute(id, builder));
    }

    /// <summary>
    /// Pops the current route and returns to the previous screen.
    /// Does nothing if already at the root.
    /// </summary>
    /// <returns>True if a route was popped, false if already at root.</returns>
    public bool Pop()
    {
        if (_navigationStack.Count <= 1)
            return false;

        _navigationStack.Pop();
        // Set pending focus restore from the entry we're returning to
        PendingFocusRestore = _navigationStack.Peek().SavedFocusIndex;
        OnNavigated?.Invoke();
        return true;
    }

    /// <summary>
    /// Pops all routes and returns to the root route.
    /// Useful for completing a wizard flow.
    /// </summary>
    public void PopToRoot()
    {
        while (_navigationStack.Count > 1)
        {
            _navigationStack.Pop();
        }
        PendingFocusRestore = _navigationStack.Peek().SavedFocusIndex;
        OnNavigated?.Invoke();
    }

    /// <summary>
    /// Replaces the current route with a new one (useful for redirects).
    /// </summary>
    /// <param name="route">The route to replace the current route with.</param>
    public void Replace(NavigatorRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (_navigationStack.Count > 0)
        {
            _navigationStack.Pop();
        }
        _navigationStack.Push(new NavigatorStackEntry(route));
        PendingFocusRestore = null;
        OnNavigated?.Invoke();
    }

    /// <summary>
    /// Replaces the current route using a simple builder function.
    /// </summary>
    /// <param name="id">Unique identifier for this route.</param>
    /// <param name="builder">Function that builds the widget for this route.</param>
    public void Replace(string id, Func<NavigatorState, Hex1bWidget> builder)
    {
        Replace(new NavigatorRoute(id, builder));
    }

    /// <summary>
    /// Resets the navigator to a new root route, clearing all history.
    /// </summary>
    /// <param name="newRoot">The new root route.</param>
    public void Reset(NavigatorRoute newRoot)
    {
        ArgumentNullException.ThrowIfNull(newRoot);
        _navigationStack.Clear();
        _navigationStack.Push(new NavigatorStackEntry(newRoot));
        PendingFocusRestore = null;
        OnNavigated?.Invoke();
    }

    /// <summary>
    /// Gets the current route's widget.
    /// </summary>
    internal Hex1bWidget BuildCurrentWidget()
    {
        return CurrentRoute.Builder(this);
    }
}

/// <summary>
/// A navigator widget that provides stack-based navigation for building 
/// wizard-style flows and drill-down experiences.
/// 
/// The navigator maintains a stack of routes. Use the NavigatorState to:
/// - Push: Navigate to a new screen (drill down)
/// - Pop: Go back to the previous screen
/// - PopToRoot: Return to the starting screen (complete wizard)
/// - Replace: Swap the current screen without adding to history
/// </summary>
[Experimental("HEX1B001")]
public sealed record NavigatorWidget(NavigatorState State) : Hex1bWidget;
