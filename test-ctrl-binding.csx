using Hex1b;
using Hex1b.Input;

// Create a TextBoxNode and check its bindings
var node = new TextBoxNode { Text = "hello world", IsFocused = true };
var builder = node.BuildBindings();
var bindings = builder.Build();

Console.WriteLine($"Total bindings: {bindings.Count}");

// Find Ctrl+Arrow bindings
foreach (var b in bindings)
{
    var step = b.FirstStep;
    if ((step.Modifiers & Hex1bModifiers.Control) != 0)
    {
        Console.WriteLine($"  {b}: Key={step.Key}, Modifiers={step.Modifiers}");
    }
}

// Test matching
var ctrlLeft = new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Control);
Console.WriteLine($"\nTest event: Key={ctrlLeft.Key}, Modifiers={ctrlLeft.Modifiers}");

var trie = ChordTrie.Build(bindings);
var result = trie.Lookup(ctrlLeft);
Console.WriteLine($"Lookup result: IsNoMatch={result.IsNoMatch}, IsLeaf={result.IsLeaf}, HasChildren={result.HasChildren}");
