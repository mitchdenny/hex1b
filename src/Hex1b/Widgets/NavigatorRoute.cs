using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a screen or page that can be displayed in a <see cref="NavigatorWidget"/>.
/// </summary>
/// <param name="Id">
/// Unique identifier for this route. Used internally to detect route changes
/// and preserve widget state during navigation.
/// </param>
/// <param name="Builder">
/// Function that builds the widget for this route. Receives the <see cref="NavigatorState"/>
/// as a parameter, allowing nested navigation and access to navigation methods.
/// </param>
/// <remarks>
/// <para>
/// Routes are immutable and reusable. The same route instance can be used multiple times
/// in the navigation stack without issues.
/// </para>
/// <para>
/// The builder function is called each time the route becomes visible. This allows you to:
/// </para>
/// <list type="bullet">
/// <item><description>Access current application state to build the UI</description></item>
/// <item><description>Use the navigator parameter to navigate to other routes</description></item>
/// <item><description>Create nested navigators for sub-flows</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Simple static route:</para>
/// <code>
/// var route = new NavigatorRoute(
///     "welcome",
///     nav => ctx.VStack(v => [
///         v.Text("Welcome!"),
///         v.Button("Continue").OnClick(_ => nav.Push(nextRoute))
///     ])
/// );
/// </code>
/// <para>Route with dynamic content based on app state:</para>
/// <code>
/// var detailRoute = new NavigatorRoute(
///     "customer-detail",
///     nav => {
///         var customer = GetSelectedCustomer();
///         return ctx.VStack(v => [
///             v.Text($"Customer: {customer.Name}"),
///             v.Text($"Email: {customer.Email}"),
///             v.Button("Edit").OnClick(_ => nav.Push(editRoute))
///         ]);
///     }
/// );
/// </code>
/// <para>Route that navigates immediately based on condition:</para>
/// <code>
/// var splashRoute = new NavigatorRoute(
///     "splash",
///     nav => {
///         if (isFirstRun) {
///             return BuildFirstRunUI(nav);
///         }
///         return BuildHomeUI(nav);
///     }
/// );
/// </code>
/// </example>
/// <seealso cref="NavigatorWidget"/>
/// <seealso cref="NavigatorState"/>
[Experimental("HEX1B001")]
public record NavigatorRoute(string Id, Func<NavigatorState, Hex1bWidget> Builder);
