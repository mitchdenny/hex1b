namespace Hex1b.Input;

/// <summary>
/// Routes input through the node tree to the focused node using layered chord tries.
/// </summary>
public static class InputRouter
{
    /// <summary>
    /// Debug: The last path that was built during input routing.
    /// </summary>
    public static string? LastPathDebug { get; private set; }
    
    /// <summary>
    /// Debug: The key that was last routed and whether a match was found.
    /// </summary>
    public static string? LastRouteDebug { get; private set; }
    
    /// <summary>
    /// Routes a key event through the node tree using layered chord tries.
    /// 
    /// Algorithm:
    /// 1. Check global bindings first (from entire tree, evaluated regardless of focus)
    /// 2. Build path from root to focused node
    /// 3. If mid-chord, validate path matches and continue chord
    /// 4. Build tries from each node's bindings (focused first, root last)
    /// 5. Search layers in order - first match wins
    /// 6. If internal node matched, start/continue chord
    /// 7. If leaf matched, execute action
    /// 8. If no match, fall through to HandleInput on focused node, then bubble up
    /// </summary>
    public static async Task<InputResult> RouteInputAsync(
        Hex1bNode root, 
        Hex1bKeyEvent keyEvent, 
        FocusRing focusRing,
        InputRouterState state,
        Action? requestStop = null,
        CancellationToken cancellationToken = default,
        Action<string>? copyToClipboard = null)
    {
        // Create the action context for this input routing
        var actionContext = new InputBindingActionContext(focusRing, requestStop, cancellationToken, copyToClipboard: copyToClipboard);
        
        // Check global bindings first (evaluated regardless of focus)
        // Global bindings are collected from the entire tree
        var globalResult = await TryHandleGlobalBindingsAsync(root, keyEvent, actionContext, state);
        if (globalResult == InputResult.Handled)
        {
            return InputResult.Handled;
        }
        
        // Build path from root to focused node
        var path = BuildPathToFocused(root);
        
        // Debug: capture path info
        LastPathDebug = $"Path ({path.Count} nodes): [{string.Join(" -> ", path.Select(n => n.GetType().Name))}]";
        
        if (path.Count == 0)
        {
            // No focused node found, nothing to route to
            state.Reset();
            LastRouteDebug = $"Key {keyEvent.Key}: No path found, NotHandled";
            return InputResult.NotHandled;
        }

        // Escape always cancels pending chord
        if (keyEvent.Key == Hex1bKey.Escape && state.ChordNode != null)
        {
            state.Reset();
            return InputResult.Handled;
        }

        // If mid-chord but focus changed, cancel the chord
        if (state.ChordNode != null && !PathsMatch(state.ChordAnchorPath, path))
        {
            state.Reset();
        }

        // If mid-chord, continue from the anchored layer
        if (state.ChordNode != null)
        {
            return await ContinueChordAsync(keyEvent, path, actionContext, state);
        }

        // Build layers: focused first (index 0), root last
        // Search from focused toward root - first match wins
        for (int i = path.Count - 1; i >= 0; i--)
        {
            var node = path[i];
            var builder = node.BuildBindings();
            var bindings = builder.Build();
            
            if (bindings.Count > 0)
            {
                var trie = ChordTrie.Build(bindings);
                var result = trie.Lookup(keyEvent);
                
                if (!result.IsNoMatch)
                {
                    // Found a match at this layer
                    if (result.IsLeaf)
                    {
                        // Leaf node - execute and done
                        await result.ExecuteAsync(actionContext);
                        state.Reset();
                        return InputResult.Handled;
                    }
                    
                    if (result.HasChildren)
                    {
                        // Internal node - start a chord, anchor to this layer
                        state.ChordNode = result.Node;
                        state.ChordAnchorPath = path;
                        state.ChordLayerIndex = i;
                        state.NotifyChordStateChanged();
                        return InputResult.Handled;  // waiting for more keys
                    }
                }
            }
            
            // No key binding matched at this layer, try character bindings
            // Character bindings only trigger on the focused node (not bubbling)
            if (i == path.Count - 1 && await TryHandleCharacterBindingAsync(builder.CharacterBindings, keyEvent, actionContext))
            {
                return InputResult.Handled;
            }
        }

        // No binding matched, let the focused node handle the input directly
        // Only call HandleInput on nodes that are actually focused
        var lastNode = path[^1];
        if (lastNode.IsFocusable && lastNode.IsFocused)
        {
            var inputResult = lastNode.HandleInput(keyEvent);
            if (inputResult == InputResult.Handled)
            {
                return inputResult;
            }

            // Focused node didn't handle it, bubble UP to container nodes
            for (int i = path.Count - 2; i >= 0; i--)
            {
                inputResult = path[i].HandleInput(keyEvent);
                if (inputResult == InputResult.Handled)
                {
                    return inputResult;
                }
            }
        }

        return InputResult.NotHandled;
    }
    
    private static async Task<InputResult> ContinueChordAsync(
        Hex1bKeyEvent keyEvent, 
        List<Hex1bNode> path, 
        InputBindingActionContext actionContext,
        InputRouterState state)
    {
        var result = state.ChordNode!.Lookup(keyEvent);
        
        if (result.IsNoMatch)
        {
            // Chord failed - no match for this key
            state.Reset();
            return InputResult.Handled;  // swallow the key
        }
        
        if (result.IsLeaf)
        {
            // Chord completed - execute
            await result.ExecuteAsync(actionContext);
            state.Reset();
            return InputResult.Handled;
        }
        
        if (result.HasChildren)
        {
            // Chord continues
            state.ChordNode = result.Node;
            state.NotifyChordStateChanged();
            return InputResult.Handled;
        }
        
        // Has action but also children - execute and reset
        // (rare case: ambiguous binding like 'g' with action and 'gg' chord)
        if (result.HasAction)
        {
            await result.ExecuteAsync(actionContext);
            state.Reset();
            return InputResult.Handled;
        }
        
        return InputResult.NotHandled;
    }
    
    private static bool PathsMatch(List<Hex1bNode>? a, List<Hex1bNode>? b)
    {
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Routes input to a single node using its bindings. 
    /// This is a simplified entry point for testing that doesn't require a full tree.
    /// </summary>
    /// <param name="node">The node to route input to (assumed to be focused).</param>
    /// <param name="keyEvent">The key event to route.</param>
    /// <param name="focusRing">Optional focus ring for context-aware bindings.</param>
    /// <param name="requestStop">Optional callback to request application stop.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <param name="copyToClipboard">Optional callback to copy text to clipboard.</param>
    /// <returns>The result of input processing.</returns>
    public static async Task<InputResult> RouteInputToNodeAsync(
        Hex1bNode node, 
        Hex1bKeyEvent keyEvent, 
        FocusRing? focusRing = null, 
        Action? requestStop = null,
        CancellationToken cancellationToken = default,
        Action<string>? copyToClipboard = null)
    {
        // Create focus ring if not provided
        focusRing ??= new FocusRing();
        var actionContext = new InputBindingActionContext(focusRing, requestStop, cancellationToken, copyToClipboard: copyToClipboard);
        
        // Build bindings for this node and look up the key
        var builder = node.BuildBindings();
        var bindings = builder.Build();
        
        if (bindings.Count > 0)
        {
            var trie = ChordTrie.Build(bindings);
            var result = trie.Lookup(keyEvent);
            
            if (result.IsLeaf)
            {
                await result.ExecuteAsync(actionContext);
                return InputResult.Handled;
            }
            
            // For chords in single-node testing, just return handled if we hit an internal node
            if (result.HasChildren)
            {
                return InputResult.Handled;
            }
        }
        
        // No key binding matched, try character bindings (only if node is focusable and focused)
        // This matches real routing where we only reach focused nodes
        if (node.IsFocusable && node.IsFocused && await TryHandleCharacterBindingAsync(builder.CharacterBindings, keyEvent, actionContext))
        {
            return InputResult.Handled;
        }
        
        // No binding matched, fall through to HandleInput
        return node.HandleInput(keyEvent);
    }
    
    /// <summary>
    /// Tries to find and execute a matching character binding.
    /// </summary>
    private static async Task<bool> TryHandleCharacterBindingAsync(
        IReadOnlyList<CharacterBinding> characterBindings, 
        Hex1bKeyEvent keyEvent,
        InputBindingActionContext actionContext)
    {
        if (characterBindings.Count == 0) return false;
        
        var text = keyEvent.Text;
        if (string.IsNullOrEmpty(text)) return false;  // No text in this event
        
        // Check each character binding in order (first match wins)
        foreach (var binding in characterBindings)
        {
            if (binding.Matches(text))
            {
                await binding.ExecuteAsync(text, actionContext);
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Builds a path from the root node to the currently focused node.
    /// If no focused node is found, builds a path that includes all nodes with bindings.
    /// Returns empty list only if the tree is empty.
    /// </summary>
    private static List<Hex1bNode> BuildPathToFocused(Hex1bNode root)
    {
        var path = new List<Hex1bNode>();
        if (!BuildPathRecursive(root, path))
        {
            // When no focused node exists, build a path that includes all nodes with bindings.
            // This ensures that bindings on non-focusable nodes (like VStack with user bindings)
            // are still checked during input routing.
            path.Clear();
            BuildPathWithBindings(root, path);
            
            // Fallback to just root if we didn't find any nodes with bindings
            if (path.Count == 0)
            {
                path.Add(root);
            }
        }
        return path;
    }

    private static bool BuildPathRecursive(Hex1bNode node, List<Hex1bNode> path)
    {
        path.Add(node);

        // If this node is focusable and focused, we found our target
        if (node.IsFocusable && node.IsFocused)
        {
            return true;
        }

        // Check children
        foreach (var child in node.GetChildren())
        {
            if (BuildPathRecursive(child, path))
            {
                return true;
            }
        }

        // No focused node found in this subtree, backtrack
        path.RemoveAt(path.Count - 1);
        return false;
    }
    
    /// <summary>
    /// Builds a path that includes all nodes with bindings (when no focused node exists).
    /// This traverses the deepest path and includes all nodes along the way.
    /// </summary>
    private static void BuildPathWithBindings(Hex1bNode node, List<Hex1bNode> path)
    {
        // Always include this node in the path - we want to check bindings on all nodes
        // from root to the deepest leaf, not just nodes that explicitly have bindings.
        // This matches the behavior when a focused node exists (entire path is checked).
        path.Add(node);
        
        // Traverse to first child (depth-first) to build a path through the tree
        var children = node.GetChildren().ToList();
        if (children.Count > 0)
        {
            BuildPathWithBindings(children[0], path);
        }
    }
    
    /// <summary>
    /// Tries to handle input via global bindings collected from the entire tree.
    /// Global bindings are checked before focus-based routing.
    /// </summary>
    private static async Task<InputResult> TryHandleGlobalBindingsAsync(
        Hex1bNode root,
        Hex1bKeyEvent keyEvent,
        InputBindingActionContext actionContext,
        InputRouterState state)
    {
        // Collect all global bindings from the entire tree
        var globalBindings = new List<InputBinding>();
        CollectGlobalBindings(root, globalBindings);
        
        if (globalBindings.Count == 0)
        {
            return InputResult.NotHandled;
        }
        
        // Build a trie from global bindings and check for a match
        var trie = ChordTrie.Build(globalBindings);
        var result = trie.Lookup(keyEvent);
        
        if (!result.IsNoMatch)
        {
            if (result.IsLeaf)
            {
                // Global binding matched - execute and done
                await result.ExecuteAsync(actionContext);
                state.Reset();
                return InputResult.Handled;
            }
            
            if (result.HasChildren)
            {
                // Global chord started - but we don't support global chords yet
                // For now, treat as handled to prevent the key from being processed elsewhere
                // TODO: Add support for global chords if needed
                return InputResult.Handled;
            }
        }
        
        return InputResult.NotHandled;
    }
    
    /// <summary>
    /// Gets all currently registered global bindings from the tree.
    /// Useful for checking conflicts during accelerator assignment.
    /// </summary>
    /// <param name="root">The root node of the tree.</param>
    /// <param name="excludeNode">Optional node to exclude from collection (to avoid self-conflict).</param>
    /// <returns>List of global bindings.</returns>
    public static IReadOnlyList<InputBinding> GetGlobalBindings(Hex1bNode root, Hex1bNode? excludeNode = null)
    {
        var bindings = new List<InputBinding>();
        CollectGlobalBindingsExcluding(root, bindings, excludeNode);
        return bindings;
    }
    
    /// <summary>
    /// Checks if a global binding with the given key step already exists.
    /// </summary>
    public static bool IsGlobalBindingRegistered(Hex1bNode root, Hex1bKey key, Hex1bModifiers modifiers, Hex1bNode? excludeNode = null)
    {
        var bindings = GetGlobalBindings(root, excludeNode);
        return bindings.Any(b => b.FirstStep.Key == key && b.FirstStep.Modifiers == modifiers);
    }
    
    /// <summary>
    /// Recursively collects global bindings, optionally excluding a node.
    /// </summary>
    private static void CollectGlobalBindingsExcluding(Hex1bNode node, List<InputBinding> bindings, Hex1bNode? excludeNode)
    {
        if (ReferenceEquals(node, excludeNode))
        {
            // Skip this node and its children
            return;
        }
        
        var builder = node.BuildBindings();
        var nodeBindings = builder.Build();
        
        foreach (var binding in nodeBindings)
        {
            if (binding.IsGlobal)
            {
                binding.OwnerNode = node;
                bindings.Add(binding);
            }
        }
        
        // Recurse into children
        foreach (var child in node.GetChildren())
        {
            CollectGlobalBindingsExcluding(child, bindings, excludeNode);
        }
    }
    
    /// <summary>
    /// Recursively collects all global bindings from the node tree.
    /// Throws on conflict for first key step.
    /// </summary>
    private static void CollectGlobalBindings(Hex1bNode node, List<InputBinding> bindings)
    {
        var builder = node.BuildBindings();
        var nodeBindings = builder.Build();
        
        foreach (var binding in nodeBindings)
        {
            if (binding.IsGlobal)
            {
                binding.OwnerNode = node;
                
                // Check for conflict with existing bindings
                var firstStep = binding.FirstStep;
                foreach (var existing in bindings)
                {
                    if (existing.FirstStep.Key == firstStep.Key && 
                        existing.FirstStep.Modifiers == firstStep.Modifiers)
                    {
                        var ownerName = existing.OwnerNode?.GetType().Name ?? "unknown";
                        var newOwnerName = node.GetType().Name;
                        throw new InvalidOperationException(
                            $"Global binding conflict: {firstStep} is already registered by {ownerName} " +
                            $"('{existing.Description}'). Cannot register it again from {newOwnerName} " +
                            $"('{binding.Description}'). Use .DisableAccelerator() or .Accelerator() to resolve.");
                    }
                }
                
                bindings.Add(binding);
            }
        }
        
        // Recurse into children
        foreach (var child in node.GetChildren())
        {
            CollectGlobalBindings(child, bindings);
        }
    }
}
