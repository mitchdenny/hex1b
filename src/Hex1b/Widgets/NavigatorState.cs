using System.Diagnostics.CodeAnalysis;

namespace Hex1b.Widgets;

/// <summary>
/// State object for <see cref="NavigatorWidget"/> that manages the navigation stack.
/// </summary>
/// <remarks>
/// <para>
/// NavigatorState is a mutable state object that controls navigation in your application.
/// Create one instance and pass it to the Navigator widget. Keep a reference to it so you
/// can call navigation methods from event handlers.
/// </para>
/// <para>
/// The navigator maintains a stack of routes. The current route is always at the top of the stack.
/// When you navigate, the focus state of the current route is automatically saved so it can be
/// restored when returning to that route.
/// </para>
/// <para>
/// <strong>Navigation Methods:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Push(NavigatorRoute)"/> - Add a new route to the stack (drill down)</description></item>
/// <item><description><see cref="Pop"/> - Remove the top route and return to the previous one (go back)</description></item>
/// <item><description><see cref="PopToRoot"/> - Remove all routes except the root (finish wizard)</description></item>
/// <item><description><see cref="Replace(NavigatorRoute)"/> - Replace the current route without affecting history</description></item>
/// <item><description><see cref="Reset(NavigatorRoute)"/> - Clear all history and start fresh with a new root</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Basic setup and navigation:</para>
/// <code>
/// // Create navigator with initial route
/// var nav = new NavigatorState(
///     new NavigatorRoute("home", n => BuildHomeScreen(n))
/// );
/// 
/// // Use in widget tree
/// var widget = ctx.Navigator(nav);
/// 
/// // Navigate from event handler
/// button.OnClick(_ => nav.Push("detail", n => BuildDetailScreen(n)));
/// </code>
/// <para>Multi-step wizard:</para>
/// <code>
/// var nav = new NavigatorState(
///     new NavigatorRoute("step1", n => 
///         ctx.VStack(v => [
///             v.Text("Step 1 of 3"),
///             v.Button("Next").OnClick(_ => 
///                 n.Push("step2", _ => BuildStep2(n)))
///         ])
///     )
/// );
/// 
/// // In step 3, complete the wizard
/// nav.PopToRoot(); // Returns to step 1
/// </code>
/// <para>Checking if navigation is possible:</para>
/// <code>
/// // Conditionally show back button
/// if (nav.CanGoBack) {
///     v.Button("Back").OnClick(_ => nav.Pop());
/// }
/// 
/// // Show current depth
/// v.Text($"Level: {nav.Depth}");
/// </code>
/// <para>Listening to navigation events:</para>
/// <code>
/// nav.OnNavigated += () => {
///     Console.WriteLine($"Navigated to: {nav.CurrentRoute.Id}");
///     LogAnalyticsEvent("page_view", nav.CurrentRoute.Id);
/// };
/// </code>
/// </example>
/// <seealso cref="NavigatorWidget"/>
/// <seealso cref="NavigatorRoute"/>
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
    /// <remarks>
    /// <para>
    /// This property returns the route that is currently being displayed. When you navigate,
    /// this property changes to reflect the new current route.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Access current route ID
    /// Console.WriteLine($"Current screen: {nav.CurrentRoute.Id}");
    /// 
    /// // Conditional logic based on current route
    /// if (nav.CurrentRoute.Id == "checkout") {
    ///     // Show checkout-specific UI
    /// }
    /// </code>
    /// </example>
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
    /// <remarks>
    /// <para>
    /// The depth is always at least 1 (the root route). Each call to <see cref="Push(NavigatorRoute)"/>
    /// increases the depth by 1, and each successful <see cref="Pop"/> decreases it by 1.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Show breadcrumb indicator
    /// v.Text($"Page {nav.Depth} of wizard");
    /// 
    /// // Limit navigation depth
    /// if (nav.Depth &lt; 5) {
    ///     nav.Push(nextRoute);
    /// } else {
    ///     ShowError("Maximum depth reached");
    /// }
    /// </code>
    /// </example>
    public int Depth => _navigationStack.Count;

    /// <summary>
    /// Returns true if there are routes to pop (more than just the root).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this property to determine whether calling <see cref="Pop"/> will have any effect.
    /// It's commonly used to conditionally show a back button.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Conditionally show back button
    /// if (nav.CanGoBack) {
    ///     v.Button("← Back").OnClick(_ => nav.Pop());
    /// }
    /// 
    /// // Disable back button
    /// v.Button("Back")
    ///     .OnClick(_ => nav.Pop())
    ///     .Disabled(!nav.CanGoBack);
    /// </code>
    /// </example>
    public bool CanGoBack => _navigationStack.Count > 1;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event is triggered after any navigation operation: <see cref="Push(NavigatorRoute)"/>,
    /// <see cref="Pop"/>, <see cref="PopToRoot"/>, <see cref="Replace(NavigatorRoute)"/>, or
    /// <see cref="Reset(NavigatorRoute)"/>.
    /// </para>
    /// <para>
    /// Use this event for side effects like logging, analytics, or updating other application state.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// nav.OnNavigated += () => {
    ///     // Log page view
    ///     logger.LogInformation("Navigated to {Route}", nav.CurrentRoute.Id);
    ///     
    ///     // Analytics
    ///     analytics.TrackPageView(nav.CurrentRoute.Id, nav.Depth);
    ///     
    ///     // Update breadcrumb state
    ///     UpdateBreadcrumbs(nav);
    /// };
    /// </code>
    /// </example>
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
    /// <remarks>
    /// <para>
    /// Push adds the new route to the top of the stack and makes it the current route.
    /// The focus state of the previous route is automatically saved so it can be restored
    /// when returning via <see cref="Pop"/>.
    /// </para>
    /// <para>
    /// This method triggers the <see cref="OnNavigated"/> event after the navigation completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var detailRoute = new NavigatorRoute("detail", nav => BuildDetailScreen(nav));
    /// navigator.Push(detailRoute);
    /// </code>
    /// </example>
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
    /// <remarks>
    /// <para>
    /// This is a convenience method that creates a <see cref="NavigatorRoute"/> and pushes it
    /// in one call. Equivalent to creating a route manually and calling <see cref="Push(NavigatorRoute)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Inline route definition
    /// navigator.Push("settings", nav => 
    ///     ctx.VStack(v => [
    ///         v.Text("Settings"),
    ///         v.Button("Back").OnClick(_ => nav.Pop())
    ///     ])
    /// );
    /// </code>
    /// </example>
    public void Push(string id, Func<NavigatorState, Hex1bWidget> builder)
    {
        Push(new NavigatorRoute(id, builder));
    }

    /// <summary>
    /// Pops the current route and returns to the previous screen.
    /// </summary>
    /// <returns>True if a route was popped, false if already at root.</returns>
    /// <remarks>
    /// <para>
    /// Pop removes the top route from the stack and makes the previous route current.
    /// If already at the root route (stack depth is 1), this method does nothing and returns false.
    /// </para>
    /// <para>
    /// When popping back to a previous route, the focus is automatically restored to the element
    /// that had focus when you originally navigated away from that route.
    /// </para>
    /// <para>
    /// This method triggers the <see cref="OnNavigated"/> event after a successful pop.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Back button that only shows when not at root
    /// if (navigator.CanGoBack) {
    ///     v.Button("← Back").OnClick(_ => navigator.Pop());
    /// }
    /// 
    /// // Try to pop, handle failure
    /// if (!navigator.Pop()) {
    ///     Console.WriteLine("Already at root, cannot go back");
    /// }
    /// </code>
    /// </example>
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
    /// </summary>
    /// <remarks>
    /// <para>
    /// PopToRoot removes all routes from the stack except the root route, making the root
    /// route current again. This is useful for completing multi-step wizards or resetting
    /// to the home screen.
    /// </para>
    /// <para>
    /// If already at the root route, this method does nothing but still triggers the
    /// <see cref="OnNavigated"/> event.
    /// </para>
    /// <para>
    /// The focus is restored to the saved state of the root route (if any).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Complete a multi-step wizard
    /// ctx.Button("Finish").OnClick(_ => {
    ///     SaveWizardData();
    ///     navigator.PopToRoot(); // Return to home screen
    /// });
    /// 
    /// // Cancel button that works from any depth
    /// ctx.Button("Cancel").OnClick(_ => {
    ///     navigator.PopToRoot(); // Abandon wizard and return home
    /// });
    /// </code>
    /// </example>
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
    /// Replaces the current route with a new one.
    /// </summary>
    /// <param name="route">The route to replace the current route with.</param>
    /// <remarks>
    /// <para>
    /// Replace swaps the current route (at the top of the stack) with a new route without
    /// changing the stack depth. The replaced route's history is discarded.
    /// </para>
    /// <para>
    /// This is useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Redirects after authentication or validation</description></item>
    /// <item><description>Preventing users from going back to a completed step</description></item>
    /// <item><description>Updating the current view without adding to history</description></item>
    /// </list>
    /// <para>
    /// This method triggers the <see cref="OnNavigated"/> event after the replacement.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Redirect from splash to home after checking login
    /// if (isLoggedIn) {
    ///     navigator.Replace(homeRoute); // Can't go back to splash
    /// } else {
    ///     navigator.Replace(loginRoute);
    /// }
    /// 
    /// // Replace form with success screen after save
    /// SaveData();
    /// navigator.Replace(successRoute); // Back button skips the form
    /// </code>
    /// </example>
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
    /// <remarks>
    /// <para>
    /// This is a convenience method that creates a <see cref="NavigatorRoute"/> and replaces
    /// the current route in one call. Equivalent to creating a route manually and calling
    /// <see cref="Replace(NavigatorRoute)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Inline replacement
    /// navigator.Replace("success", nav => 
    ///     ctx.VStack(v => [
    ///         v.Text("✓ Saved successfully!"),
    ///         v.Button("OK").OnClick(_ => nav.PopToRoot())
    ///     ])
    /// );
    /// </code>
    /// </example>
    public void Replace(string id, Func<NavigatorState, Hex1bWidget> builder)
    {
        Replace(new NavigatorRoute(id, builder));
    }

    /// <summary>
    /// Resets the navigator to a new root route, clearing all history.
    /// </summary>
    /// <param name="newRoot">The new root route.</param>
    /// <remarks>
    /// <para>
    /// Reset clears the entire navigation stack and starts fresh with a new root route.
    /// All previous navigation history is discarded. This is useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Logging out and returning to login screen</description></item>
    /// <item><description>Switching between major app sections</description></item>
    /// <item><description>Restarting a flow from the beginning</description></item>
    /// </list>
    /// <para>
    /// This method triggers the <see cref="OnNavigated"/> event after the reset.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Logout and return to login screen
    /// ctx.Button("Logout").OnClick(_ => {
    ///     ClearUserSession();
    ///     navigator.Reset(loginRoute);
    /// });
    /// 
    /// // Switch between app sections
    /// ctx.Button("Go to Admin").OnClick(_ => {
    ///     navigator.Reset(adminHomeRoute);
    /// });
    /// </code>
    /// </example>
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
