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
