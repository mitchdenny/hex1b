using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A one-line status bar widget, typically placed at the bottom of the screen to display
/// application status, keyboard shortcuts, or contextual information.
/// </summary>
/// <param name="Sections">The sections to display in the info bar. Each section can have custom colors.</param>
/// <param name="InvertColors">
/// Whether to invert foreground/background colors from the theme (default: true).
/// When true, creates a visually distinct bar by swapping the theme's foreground and background colors.
/// </param>
/// <remarks>
/// <para>
/// InfoBar is designed to be placed at the bottom of the screen as a status indicator,
/// similar to status bars in editors like Vim or Visual Studio Code. It always measures
/// to exactly one line in height and fills the available width.
/// </para>
/// <para>
/// By default, InfoBar renders with inverted colors (InvertColors = true), which swaps
/// the theme's foreground and background colors to create a visually distinct bar. Individual
/// sections can override these colors using <see cref="InfoBarSection.Foreground"/> and
/// <see cref="InfoBarSection.Background"/>.
/// </para>
/// <para>
/// InfoBar is not focusable and does not handle input. It is purely a display widget for
/// conveying information to the user.
/// </para>
/// </remarks>
/// <example>
/// <para>Simple status bar with a single message:</para>
/// <code>
/// var app = new Hex1bApp(ctx =&gt; Task.FromResult&lt;Hex1bWidget&gt;(
///     ctx.VStack(v => [
///         v.Text("Main content area"),
///         v.InfoBar("Ready")
///     ])
/// ));
/// 
/// await app.RunAsync();
/// </code>
/// <para>Status bar with multiple sections showing shortcuts:</para>
/// <code>
/// var app = new Hex1bApp(ctx =&gt; Task.FromResult&lt;Hex1bWidget&gt;(
///     ctx.VStack(v => [
///         v.Border(b => [
///             b.Text("Application content")
///         ], title: "My App").Fill(),
///         v.InfoBar([
///             "F1", "Help",
///             "Ctrl+S", "Save",
///             "Ctrl+Q", "Quit"
///         ])
///     ])
/// ));
/// 
/// await app.RunAsync();
/// </code>
/// <para>Status bar with custom colored sections:</para>
/// <code>
/// using Hex1b;
/// using Hex1b.Theming;
/// using Hex1b.Widgets;
/// 
/// var app = new Hex1bApp(ctx =&gt; Task.FromResult&lt;Hex1bWidget&gt;(
///     ctx.VStack(v => [
///         v.Text("Main area"),
///         v.InfoBar([
///             new InfoBarSection("Mode: Normal"),
///             new InfoBarSection(" | "),
///             new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow),
///             new InfoBarSection(" | "),
///             new InfoBarSection("Ln 42, Col 7")
///         ])
///     ])
/// ));
/// 
/// await app.RunAsync();
/// </code>
/// </example>
/// <seealso cref="InfoBarSection"/>
/// <seealso cref="InfoBarExtensions"/>
public sealed record InfoBarWidget(
    IReadOnlyList<InfoBarSection> Sections,
    bool InvertColors = true) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as InfoBarNode ?? new InfoBarNode();
        node.Sections = Sections;
        node.InvertColors = InvertColors;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(InfoBarNode);
}
