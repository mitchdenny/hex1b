// Copyright (c) Hex1b contributors. Licensed under the MIT license.
//
// Repo-wide test helper that bridges xUnit's `Assert.Equal(coll, coll)` semantics
// (sequence equality) to MSTest. MSTest's own `Assert.AreEqual` uses
// `EqualityComparer<T>.Default` which on collection types collapses to reference
// equality, while `CollectionAssert.AreEqual` requires `ICollection` rather than
// `IEnumerable<T>`. `TestSeq` papers over both: it materialises both sides via
// `ToList()` and delegates to `CollectionAssert`.
//
// The xunit -> mstest codemod (`eng/codemod/xunit_to_mstest.py`) rewrites
// `Assert.Equal(a, b)` to `TestSeq.AreEqual(a, b)` whenever either argument
// looks like a collection literal, LINQ chain, or collection-typed expression.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Testing;

internal static class TestSeq
{
    public static void AreEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, string? message = null)
        => CollectionAssert.AreEqual(expected?.ToList(), actual?.ToList(), message ?? string.Empty);

    public static void AreNotEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, string? message = null)
        => CollectionAssert.AreNotEqual(notExpected?.ToList(), actual?.ToList(), message ?? string.Empty);

    /// <summary>
    /// xUnit's <c>Assert.Single(coll)</c> returns the single element. MSTest's
    /// <c>Assert.HasCount(1, coll)</c> returns void, so the codemod routes
    /// `var x = Assert.Single(coll)` to `var x = TestSeq.Single(coll)`.
    /// </summary>
    public static T Single<T>(IEnumerable<T> source)
    {
        Assert.HasCount(1, source);
        return source.Single();
    }

    /// <summary>
    /// xUnit's <c>Assert.IsType&lt;T&gt;(value)</c> returns the cast value.
    /// MSTest's <c>Assert.IsInstanceOfType&lt;T&gt;(value)</c> returns void,
    /// so the codemod routes `var x = Assert.IsType&lt;T&gt;(v)` to
    /// `var x = TestSeq.IsType&lt;T&gt;(v)`.
    /// </summary>
    public static T IsType<T>(object? value)
    {
        Assert.IsInstanceOfType<T>(value);
        return (T)value!;
    }

    /// <summary>
    /// xUnit's <c>Assert.Single(coll, predicate)</c>: assert exactly one
    /// matching element exists, and return it.
    /// </summary>
    public static T Single<T>(IEnumerable<T> source, Func<T, bool> predicate)
    {
        var matches = source.Where(predicate).ToList();
        Assert.HasCount(1, matches);
        return matches[0];
    }

    /// <summary>
    /// xUnit's <c>Assert.All(coll, action)</c>. MSTest has no equivalent;
    /// emulate by invoking the assertion action for every element.
    /// </summary>
    public static void All<T>(IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }

    /// <summary>
    /// xUnit's <c>Assert.Collection(coll, e1, e2, ...)</c>: assert the
    /// collection has exactly <c>elementInspectors.Length</c> items, then
    /// apply each inspector to the matching element by index.
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

    /// <summary>xUnit's <c>Assert.InRange(value, low, high)</c>.</summary>
    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        Assert.IsTrue(
            actual.CompareTo(low) >= 0 && actual.CompareTo(high) <= 0,
            $"Expected {actual} to be in range [{low}, {high}].");
    }

    /// <summary>xUnit's <c>Assert.Matches(pattern, input)</c>.</summary>
    public static void Matches(string pattern, string? input)
    {
        Assert.IsNotNull(input);
        Assert.IsTrue(
            System.Text.RegularExpressions.Regex.IsMatch(input, pattern),
            $"Expected input to match pattern '{pattern}'. Actual: {input}");
    }

    /// <summary>xUnit's <c>Record.Exception(action)</c> — capture an exception or return null.</summary>
    public static Exception? RecordException(Action action)
    {
        try { action(); return null; }
        catch (Exception ex) { return ex; }
    }

    public static async Task<Exception?> RecordExceptionAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }
}

/// <summary>
/// xUnit-style TheoryData shim. MSTest's <c>[DynamicData]</c> consumes
/// <c>IEnumerable&lt;object[]&gt;</c>, so this acts as a drop-in replacement
/// for xUnit's <c>TheoryData&lt;T&gt;</c> via collection-initializer syntax.
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
