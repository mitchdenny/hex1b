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
    /// <param name="ctx">The widget context.</param>
    /// <param name="menuBuilder">A function that builds the menus using a MenuContext.</param>
    /// <returns>A MenuBarWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.MenuBar(m => [
    ///     m.Menu("File", m => [
    ///         m.MenuItem("New").OnSelect(e => { /* action */ }),
    ///         m.MenuItem("Open").OnSelect(e => { /* action */ }),
    ///         m.Separator(),
    ///         m.MenuItem("Quit").OnSelect(e => e.CloseMenu())
    ///     ]),
    ///     m.Menu("Edit", m => [
    ///         m.MenuItem("Undo").Disabled(),
    ///         m.MenuItem("Redo").Disabled(),
    ///         m.Separator(),
    ///         m.MenuItem("Cut").OnSelect(e => { /* action */ }),
    ///         m.MenuItem("Copy").OnSelect(e => { /* action */ }),
    ///         m.MenuItem("Paste").OnSelect(e => { /* action */ })
    ///     ])
    /// ])
    /// </code>
    /// </example>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> ctx,
        Func<MenuContext, IEnumerable<MenuWidget>> menuBuilder)
        where TParent : Hex1bWidget
    {
        var menuContext = new MenuContext();
        var menus = menuBuilder(menuContext).ToList();
        return new MenuBarWidget(menus);
    }

    /// <summary>
    /// Creates a menu bar with pre-built menus.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="menus">The menus to include in the bar.</param>
    /// <returns>A MenuBarWidget.</returns>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> ctx,
        IEnumerable<MenuWidget> menus)
        where TParent : Hex1bWidget
    {
        return new MenuBarWidget(menus.ToList());
    }

    /// <summary>
    /// Creates a menu bar with pre-built menus.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="menus">The menus to include in the bar.</param>
    /// <returns>A MenuBarWidget.</returns>
    public static MenuBarWidget MenuBar<TParent>(
        this WidgetContext<TParent> ctx,
        params MenuWidget[] menus)
        where TParent : Hex1bWidget
    {
        return new MenuBarWidget(menus.ToList());
    }
}
