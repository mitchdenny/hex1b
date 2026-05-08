using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Hex1b.Analyzers;

/// <summary>
/// Shared helpers for resolving Hex1b base type symbols and walking inheritance chains.
/// </summary>
internal static class Hex1bSymbols
{
    public const string WidgetMetadataName = "Hex1b.Widgets.Hex1bWidget";
    public const string NodeMetadataName = "Hex1b.Hex1bNode";
    public const string WidgetContextMetadataName = "Hex1b.WidgetContext`1";

    /// <summary>
    /// Gets the <c>Hex1b.Widgets.Hex1bWidget</c> type symbol from the compilation, or null if not referenced.
    /// </summary>
    public static INamedTypeSymbol? GetWidgetType(Compilation compilation)
        => compilation.GetTypeByMetadataName(WidgetMetadataName);

    /// <summary>
    /// Gets the <c>Hex1b.Hex1bNode</c> type symbol from the compilation, or null if not referenced.
    /// </summary>
    public static INamedTypeSymbol? GetNodeType(Compilation compilation)
        => compilation.GetTypeByMetadataName(NodeMetadataName);

    /// <summary>
    /// Gets the unbound generic <c>Hex1b.WidgetContext&lt;TParentWidget&gt;</c> type symbol from the
    /// compilation, or null if not referenced.
    /// </summary>
    public static INamedTypeSymbol? GetWidgetContextType(Compilation compilation)
        => compilation.GetTypeByMetadataName(WidgetContextMetadataName);

    /// <summary>
    /// Returns true if <paramref name="type"/> is exactly <paramref name="target"/> or transitively
    /// inherits from it. Walks the BaseType chain. Returns false for null inputs.
    /// </summary>
    public static bool InheritsFromOrEquals(ITypeSymbol? type, INamedTypeSymbol? target)
    {
        if (type is null || target is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> represents a "widget-typed" parameter, meaning it either
    /// inherits from <paramref name="widgetType"/> directly, or it is a type parameter whose constraint
    /// chain transitively includes <paramref name="widgetType"/>.
    /// </summary>
    public static bool IsWidgetTypeOrConstrainedToWidget(ITypeSymbol? type, INamedTypeSymbol? widgetType)
    {
        if (type is null || widgetType is null)
        {
            return false;
        }

        if (InheritsFromOrEquals(type, widgetType))
        {
            return true;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            return TypeParameterIsConstrainedToWidget(typeParameter, widgetType, new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default));
        }

        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> is a constructed <c>Hex1b.WidgetContext&lt;T&gt;</c>
    /// (regardless of the specific T argument).
    /// </summary>
    public static bool IsWidgetContext(ITypeSymbol? type, INamedTypeSymbol? widgetContextType)
    {
        if (type is null || widgetContextType is null)
        {
            return false;
        }

        if (type is not INamedTypeSymbol named || !named.IsGenericType)
        {
            return false;
        }

        var definition = named.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, widgetContextType);
    }

    /// <summary>
    /// Returns true if <paramref name="parameter"/> is a "widget-builder callback" — that is,
    /// a <see cref="System.Func{T, TResult}"/> whose return type is a widget, an array of widgets,
    /// or an <see cref="System.Collections.Generic.IEnumerable{T}"/> of widgets. Used by HEX1B0008
    /// (single-builder naming) and HEX1B0009 (at-most-one-builder).
    /// </summary>
    public static bool IsWidgetBuilderCallback(IParameterSymbol parameter, INamedTypeSymbol? widgetType)
    {
        if (widgetType is null)
        {
            return false;
        }

        if (parameter.Type is not INamedTypeSymbol named || !named.IsGenericType)
        {
            return false;
        }

        // Must be System.Func`N (any arity >= 1) — i.e. delegate type whose name starts with "Func"
        // and lives in System. We accept Func<T, TResult>, Func<T1, T2, TResult>, etc. so long as
        // the final type argument is a widget shape.
        var def = named.ConstructedFrom;
        if (def.Name != "Func" || def.ContainingNamespace?.ToDisplayString() != "System")
        {
            return false;
        }

        if (named.TypeArguments.Length == 0)
        {
            return false;
        }

        var returnType = named.TypeArguments[named.TypeArguments.Length - 1];
        return ReturnTypeIsWidgetShape(returnType, widgetType);
    }

    private static bool ReturnTypeIsWidgetShape(ITypeSymbol returnType, INamedTypeSymbol widgetType)
    {
        // Strip nullable annotation: Func<..., Hex1bWidget?> still counts.
        if (returnType is INamedTypeSymbol nullable
            && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && nullable.TypeArguments.Length == 1)
        {
            returnType = nullable.TypeArguments[0];
        }

        // Direct widget return.
        if (InheritsFromOrEquals(returnType, widgetType))
        {
            return true;
        }

        // Array of widgets: Hex1bWidget[] (any element subtype).
        if (returnType is IArrayTypeSymbol array)
        {
            return InheritsFromOrEquals(array.ElementType, widgetType)
                || (array.ElementType is INamedTypeSymbol elemNullable
                    && elemNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    && elemNullable.TypeArguments.Length == 1
                    && InheritsFromOrEquals(elemNullable.TypeArguments[0], widgetType));
        }

        // IEnumerable<widget> (or any subtype of widget). Accepts IEnumerable<Hex1bWidget?> too.
        if (returnType is INamedTypeSymbol namedReturn && namedReturn.IsGenericType)
        {
            foreach (var iface in EnumerateInterfacesAndSelf(namedReturn))
            {
                if (iface.Name == "IEnumerable"
                    && iface.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
                    && iface.TypeArguments.Length == 1)
                {
                    var element = iface.TypeArguments[0];
                    if (element is INamedTypeSymbol elemNullable
                        && elemNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                        && elemNullable.TypeArguments.Length == 1)
                    {
                        element = elemNullable.TypeArguments[0];
                    }

                    if (InheritsFromOrEquals(element, widgetType))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateInterfacesAndSelf(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var iface in type.AllInterfaces)
        {
            yield return iface;
        }
    }

    private static bool TypeParameterIsConstrainedToWidget(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol widgetType,
        HashSet<ITypeParameterSymbol> visited)
    {
        if (!visited.Add(typeParameter))
        {
            return false;
        }

        foreach (var constraint in typeParameter.ConstraintTypes)
        {
            if (InheritsFromOrEquals(constraint, widgetType))
            {
                return true;
            }

            if (constraint is ITypeParameterSymbol nested
                && TypeParameterIsConstrainedToWidget(nested, widgetType, visited))
            {
                return true;
            }
        }

        return false;
    }
}
