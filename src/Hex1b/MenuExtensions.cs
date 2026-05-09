using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating menu widgets.
/// </summary>
public static class MenuExtensions
{
    /// <summary>
    /// Creates a menu bar with the specified menus.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="builder">A function that builds the menus using a MenuContext.</param>
    /// <returns>A MenuBarWidget.</returns>
    /// <example>
    /// <code>
    /// context.MenuBar(m => [
    ///     m.Menu("File", m => [
    ///         m.MenuItem("New").OnActivated(e => { /* action */ }),
    ///         m.MenuItem("Open").OnActivated(e => { /* action */ }),
    ///         m.Separator(),
    ///         m.MenuItem("Quit").OnActivated(e => e.Context.RequestStop())
    ///     ]),
    ///     m.Menu("Edit", m => [
    ///         m.MenuItem("Undo").Disabled(),
    ///         m.MenuItem("Redo").Disabled(),
    ///         m.Separator(),
    ///         m.MenuItem("Cut").OnActivated(e => { /* action */ }),
    ///         m.MenuItem("Copy").OnActivated(e => { /* action */ }),
    ///         m.MenuItem("Paste").OnActivated(e => { /* action */ })
    ///     ])
    /// ])
    /// </code>
    /// </example>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> context,
        Func<MenuContext, IEnumerable<MenuWidget>> builder)
        where TParent : Hex1bWidget
    {
        var menuContext = new MenuContext();
        var menus = builder(menuContext).ToList();
        return new MenuBarWidget(menus);
    }

    /// <summary>
    /// Creates a menu bar with pre-built menus.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="menus">The menus to include in the bar.</param>
    /// <returns>A MenuBarWidget.</returns>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> context,
        IEnumerable<MenuWidget> menus)
        where TParent : Hex1bWidget
    {
        return new MenuBarWidget(menus.ToList());
    }

    /// <summary>
    /// Creates a menu bar with pre-built menus.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="menus">The menus to include in the bar.</param>
    /// <returns>A MenuBarWidget.</returns>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> context,
        params MenuWidget[] menus)
        where TParent : Hex1bWidget
    {
        return new MenuBarWidget(menus.ToList());
    }
}
