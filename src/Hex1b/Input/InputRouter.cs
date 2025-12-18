namespace Hex1b.Input;

/// <summary>
/// Routes input through the node tree to the focused node using layered chord tries.
/// </summary>
public static class InputRouter
{
    /// <summary>
    /// Current chord state - the trie node we're in mid-chord, if any.
    /// </summary>
    private static ChordTrie? _chordNode;
    
    /// <summary>
    /// The path when the chord started (to detect focus changes).
    /// </summary>
    private static List<Hex1bNode>? _chordAnchorPath;
    
    /// <summary>
    /// The layer index of the chord (which node in the path owns the chord).
    /// </summary>
    private static int _chordLayerIndex = -1;
    
    /// <summary>
    /// Gets whether we're currently mid-chord.
    /// </summary>
    public static bool IsInChord => _chordNode != null;
    
    /// <summary>
    /// Event raised when chord state changes (for UI feedback).
    /// </summary>
    public static event Action? OnChordStateChanged;

    /// <summary>
    /// Routes a key event through the node tree using layered chord tries.
    /// 
    /// Algorithm:
    /// 1. Build path from root to focused node
    /// 2. If mid-chord, validate path matches and continue chord
    /// 3. Build tries from each node's bindings (focused first, root last)
    /// 4. Search layers in order - first match wins
    /// 5. If internal node matched, start/continue chord
    /// 6. If leaf matched, execute action
    /// 7. If no match, fall through to HandleInput on focused node, then bubble up
    /// </summary>
    public static async Task<InputResult> RouteInputAsync(
        Hex1bNode root, 
        Hex1bKeyEvent keyEvent, 
        FocusRing focusRing, 
        Action? requestStop = null,
        CancellationToken cancellationToken = default)
    {
        // Create the action context for this input routing
        var actionContext = new InputBindingActionContext(focusRing, requestStop, cancellationToken);
        
        // Build path from root to focused node
        var path = BuildPathToFocused(root);
        if (path.Count == 0)
        {
            // No focused node found, nothing to route to
            ResetChordState();
            return InputResult.NotHandled;
        }

        // Escape always cancels pending chord
        if (keyEvent.Key == Hex1bKey.Escape && _chordNode != null)
        {
            ResetChordState();
            return InputResult.Handled;
        }

        // If mid-chord but focus changed, cancel the chord
        if (_chordNode != null && !PathsMatch(_chordAnchorPath, path))
        {
            ResetChordState();
        }

        // If mid-chord, continue from the anchored layer
        if (_chordNode != null)
        {
            return await ContinueChordAsync(keyEvent, path, actionContext);
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
                        ResetChordState();
                        return InputResult.Handled;
                    }
                    
                    if (result.HasChildren)
                    {
                        // Internal node - start a chord, anchor to this layer
                        _chordNode = result.Node;
                        _chordAnchorPath = path;
                        _chordLayerIndex = i;
                        OnChordStateChanged?.Invoke();
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
        var focusedNode = path[^1];
        var inputResult = focusedNode.HandleInput(keyEvent);
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

        return InputResult.NotHandled;
    }
    
    /// <summary>
    /// Routes a key event through the node tree (legacy overload without FocusRing).
    /// Creates an empty focus ring for backward compatibility.
    /// </summary>
    [Obsolete("Use RouteInputAsync(root, keyEvent, focusRing, requestStop) for full functionality.")]
    public static Task<InputResult> RouteInputAsync(Hex1bNode root, Hex1bKeyEvent keyEvent)
    {
        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        return RouteInputAsync(root, keyEvent, focusRing, null);
    }
    
    private static async Task<InputResult> ContinueChordAsync(Hex1bKeyEvent keyEvent, List<Hex1bNode> path, InputBindingActionContext actionContext)
    {
        var result = _chordNode!.Lookup(keyEvent);
        
        if (result.IsNoMatch)
        {
            // Chord failed - no match for this key
            ResetChordState();
            return InputResult.Handled;  // swallow the key
        }
        
        if (result.IsLeaf)
        {
            // Chord completed - execute
            await result.ExecuteAsync(actionContext);
            ResetChordState();
            return InputResult.Handled;
        }
        
        if (result.HasChildren)
        {
            // Chord continues
            _chordNode = result.Node;
            OnChordStateChanged?.Invoke();
            return InputResult.Handled;
        }
        
        // Has action but also children - execute and reset
        // (rare case: ambiguous binding like 'g' with action and 'gg' chord)
        if (result.HasAction)
        {
            await result.ExecuteAsync(actionContext);
            ResetChordState();
            return InputResult.Handled;
        }
        
        return InputResult.NotHandled;
    }
    
    private static void ResetChordState()
    {
        var wasInChord = _chordNode != null;
        _chordNode = null;
        _chordAnchorPath = null;
        _chordLayerIndex = -1;
        if (wasInChord)
        {
            OnChordStateChanged?.Invoke();
        }
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
    /// <returns>The result of input processing.</returns>
    public static async Task<InputResult> RouteInputToNodeAsync(
        Hex1bNode node, 
        Hex1bKeyEvent keyEvent, 
        FocusRing? focusRing = null, 
        Action? requestStop = null,
        CancellationToken cancellationToken = default)
    {
        // Create focus ring if not provided
        focusRing ??= new FocusRing();
        var actionContext = new InputBindingActionContext(focusRing, requestStop, cancellationToken);
        
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
    /// Returns empty list if no focused node is found.
    /// </summary>
    private static List<Hex1bNode> BuildPathToFocused(Hex1bNode root)
    {
        var path = new List<Hex1bNode>();
        BuildPathRecursive(root, path);
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
}
