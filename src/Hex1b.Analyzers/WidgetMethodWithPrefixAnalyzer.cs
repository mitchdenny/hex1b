using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0001: Widget extension or instance method names must not start with <c>With</c>.
/// The <c>With*</c> prefix is reserved for the <c>Hex1bTerminalBuilder</c> fluent API.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetMethodWithPrefixAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Widget method name must not start with 'With'";

    private static readonly LocalizableString MessageFormat =
        "Method '{0}' on widget type should not start with 'With'. The 'With*' prefix is reserved for Hex1bTerminalBuilder; rename to drop the 'With' prefix (e.g. 'WithFoo' -> 'Foo').";

    private static readonly LocalizableString Description =
        "Hex1b reserves the 'With*' fluent prefix for Hex1bTerminalBuilder. Extension methods on widgets and instance methods declared on widget records should use a property-style name (e.g. 'Wrap', 'Ellipsis', 'OnClick') rather than 'WithWrap' or 'WithEllipsis'.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetMethodNameStartsWithWith,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetMethodNameStartsWithWith));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var widgetType = Hex1bSymbols.GetWidgetType(compilationStart.Compilation);
            if (widgetType is null)
            {
                return;
            }

            compilationStart.RegisterSymbolAction(
                ctx => AnalyzeMethod(ctx, widgetType),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol widgetType)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!StartsWithWithPrefix(method.Name))
        {
            return;
        }

        if (method.MethodKind is MethodKind.Constructor
            or MethodKind.PropertyGet
            or MethodKind.PropertySet
            or MethodKind.EventAdd
            or MethodKind.EventRemove)
        {
            return;
        }

        // Skip overrides and explicit interface implementations: the violation belongs to the
        // base/declaring member, not the override site.
        if (method.IsOverride || !method.ExplicitInterfaceImplementations.IsDefaultOrEmpty)
        {
            return;
        }

        // Builder-style hosts (e.g. Hex1bTerminalBuilder) legitimately use the With* prefix.
        if (IsBuilderType(method.ContainingType))
        {
            return;
        }

        if (method.IsExtensionMethod)
        {
            // Extension methods always have at least one parameter (the receiver). Defensive null check
            // covers degenerate compilation states.
            var receiverType = method.Parameters.IsDefaultOrEmpty ? null : method.Parameters[0].Type;
            if (Hex1bSymbols.IsWidgetTypeOrConstrainedToWidget(receiverType, widgetType))
            {
                Report(context, method);
            }
            return;
        }

        // Instance methods declared on a widget type.
        if (method.IsStatic)
        {
            return;
        }

        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return;
        }

        if (Hex1bSymbols.InheritsFromOrEquals(method.ContainingType, widgetType))
        {
            Report(context, method);
        }
    }

    private static bool StartsWithWithPrefix(string name)
    {
        // Match "With" followed by an uppercase letter so we do not flag a hypothetical method
        // literally named "With", or members like "Within"/"Without" that share the prefix.
        if (name.Length < 5)
        {
            return false;
        }

        if (!name.StartsWith("With", System.StringComparison.Ordinal))
        {
            return false;
        }

        return char.IsUpper(name[4]);
    }

    private static bool IsBuilderType(INamedTypeSymbol? containingType)
    {
        if (containingType is null)
        {
            return false;
        }

        for (var current = containingType; current is not null; current = current.BaseType)
        {
            if (current.Name.EndsWith("Builder", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, IMethodSymbol method)
    {
        var location = method.Locations.IsDefaultOrEmpty ? Location.None : method.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name));
    }
}
