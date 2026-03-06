using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Demo;

/// <summary>
/// A simple greeter service that demonstrates syntax highlighting.
/// </summary>
public sealed class Greeter
{
    private readonly string _name;
    private readonly List<string> _history = new();

    public Greeter(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Gets the greeting history.</summary>
    public IReadOnlyList<string> History => _history;

    public string Greet()
    {
        var greeting = $"Hello, {_name}!";
        Console.WriteLine(greeting);
        _history.Add(greeting);
        return greeting;
    }

    public async Task<List<string>> GetHistoryAsync(int count)
    {
        var results = new List<string>();
        for (var i = 0; i < count; i++)
        {
            results.Add(Greet());
            await Task.Delay(100);
        }
        return results;
    }
}

/// <summary>
/// A calculator with intentional issues for diagnostic demo.
/// </summary>
public class Calculator
{
    public int Add(int a, int b)
    {
        var unusedResult = a * b; // Warning: unused variable
        return a + b;
    }

    public int Divide(int a, int b)
    {
        // TODO: handle division by zero
        return a / b;
    }

    public double Average(IEnumerable<int> numbers)
    {
        var sum = 0.0;
        var count = 0;
        foreach (var n in numbers)
        {
            sum += n;
            count++;
        }
        return count > 0 ? sum / count : 0.0;
    }
}
