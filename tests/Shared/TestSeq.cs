// Copyright (c) Hex1b contributors. Licensed under the MIT license.
//
// Repo-wide test helpers that provide sequence-equality assertions and a few
// other value-returning shims on top of MSTest. MSTest's own Assert.AreEqual
// uses EqualityComparer<T>.Default which on collection types collapses to
// reference equality, and CollectionAssert.AreEqual requires ICollection
// rather than IEnumerable<T>. TestSeq materialises both sides via ToList()
// and delegates to CollectionAssert so collection comparisons "just work".
//
// Linked into every *.Tests project via Directory.Build.props so the source
// of truth lives once at tests/Shared/TestSeq.cs.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Testing;

internal static class TestSeq
{
    /// <summary>
    /// Sequence-equality assertion over <see cref="IEnumerable{T}"/>. Materialises
    /// both sides via <c>ToList()</c> and delegates to <see cref="CollectionAssert.AreEqual(System.Collections.ICollection, System.Collections.ICollection, string)"/>.
    /// </summary>
    public static void AreEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, string? message = null)
        => CollectionAssert.AreEqual(expected?.ToList(), actual?.ToList(), message ?? string.Empty);

    /// <summary>
    /// Sequence-inequality assertion over <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static void AreNotEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, string? message = null)
        => CollectionAssert.AreNotEqual(notExpected?.ToList(), actual?.ToList(), message ?? string.Empty);

    /// <summary>
    /// Assert that <paramref name="source"/> contains exactly one element, and return it.
    /// MSTest's <c>Assert.HasCount(1, coll)</c> returns <see langword="void"/>, so this helper
    /// wraps it and returns the single element so callers can assign / chain off it.
    /// </summary>
    public static T Single<T>(IEnumerable<T> source)
    {
        Assert.HasCount(1, source);
        return source.Single();
    }

    /// <summary>
    /// Assert that <paramref name="value"/> is of type <typeparamref name="T"/>, and return
    /// the cast value. MSTest's <c>Assert.IsInstanceOfType&lt;T&gt;(value)</c> returns
    /// <see langword="void"/>, so this helper wraps it and returns the cast value.
    /// </summary>
    public static T IsType<T>(object? value)
    {
        Assert.IsInstanceOfType<T>(value);
        return (T)value!;
    }

    /// <summary>
    /// Assert that exactly one element of <paramref name="source"/> matches
    /// <paramref name="predicate"/>, and return that element.
    /// </summary>
    public static T Single<T>(IEnumerable<T> source, Func<T, bool> predicate)
    {
        var matches = source.Where(predicate).ToList();
        Assert.HasCount(1, matches);
        return matches[0];
    }

    /// <summary>
    /// Apply <paramref name="action"/> to every element of <paramref name="source"/>.
    /// Useful for asserting that every element satisfies some property.
    /// </summary>
    public static void All<T>(IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }

    /// <summary>
    /// Assert that <paramref name="source"/> contains exactly <paramref name="elementInspectors"/>.Length
    /// items, then apply each inspector to the matching element by index.
    /// </summary>
    public static void Collection<T>(IEnumerable<T> source, params Action<T>[] elementInspectors)
    {
        var list = source.ToList();
        Assert.HasCount(elementInspectors.Length, list);
        for (var i = 0; i < elementInspectors.Length; i++)
        {
            elementInspectors[i](list[i]);
        }
    }

    /// <summary>Assert that <paramref name="actual"/> is within the inclusive range [<paramref name="low"/>, <paramref name="high"/>].</summary>
    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        Assert.IsTrue(
            actual.CompareTo(low) >= 0 && actual.CompareTo(high) <= 0,
            $"Expected {actual} to be in range [{low}, {high}].");
    }

    /// <summary>Assert that <paramref name="input"/> matches the regex <paramref name="pattern"/>.</summary>
    public static void Matches(string pattern, string? input)
    {
        Assert.IsNotNull(input);
        Assert.IsTrue(
            System.Text.RegularExpressions.Regex.IsMatch(input, pattern),
            $"Expected input to match pattern '{pattern}'. Actual: {input}");
    }

    /// <summary>Run <paramref name="action"/> and return any thrown exception, or <see langword="null"/> if it completes.</summary>
    public static Exception? RecordException(Action action)
    {
        try { action(); return null; }
        catch (Exception ex) { return ex; }
    }

    /// <summary>Async counterpart of <see cref="RecordException(Action)"/>.</summary>
    public static async Task<Exception?> RecordExceptionAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }
}

/// <summary>
/// Strongly-typed row collection consumed by <c>[DynamicData]</c>. Inherits
/// from <c>List&lt;object?[]&gt;</c> so MSTest sees each row as a parameter
/// tuple, and exposes a typed <c>Add</c> overload so test classes can use
/// collection-initializer syntax.
/// </summary>
public sealed class TheoryData<T> : List<object?[]>
{
    public void Add(T value) => base.Add(new object?[] { value });
}

public sealed class TheoryData<T1, T2> : List<object?[]>
{
    public void Add(T1 a, T2 b) => base.Add(new object?[] { a, b });
}

public sealed class TheoryData<T1, T2, T3> : List<object?[]>
{
    public void Add(T1 a, T2 b, T3 c) => base.Add(new object?[] { a, b, c });
}

public sealed class TheoryData<T1, T2, T3, T4> : List<object?[]>
{
    public void Add(T1 a, T2 b, T3 c, T4 d) => base.Add(new object?[] { a, b, c, d });
}
