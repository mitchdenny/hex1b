namespace Hex1b;

/// <summary>
/// Registry for WindowManager instances, allowing access from anywhere in the app.
/// </summary>
/// <remarks>
/// <para>
/// WindowPanels register their managers here on creation. This enables access to
/// window managers from contexts where tree walking doesn't work (e.g., popups, menus).
/// </para>
/// <para>
/// When using a single WindowPanel, no name is required. When using multiple
/// WindowPanels, each must have a unique name.
/// </para>
/// </remarks>
public sealed class WindowManagerRegistry
{
    private readonly Dictionary<string, WindowManager> _managers = new();
    private WindowManager? _defaultManager;
    private readonly object _lock = new();

    /// <summary>
    /// Registers a WindowManager with an optional name.
    /// </summary>
    /// <param name="manager">The window manager to register.</param>
    /// <param name="name">Optional name for the panel. Null for the default (unnamed) panel.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if registering an unnamed panel when named panels exist,
    /// registering a named panel when an unnamed panel exists,
    /// or registering a duplicate name.
    /// </exception>
    internal void Register(WindowManager manager, string? name)
    {
        lock (_lock)
        {
            if (name == null)
            {
                // Registering an unnamed (default) panel
                if (_managers.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Cannot register an unnamed WindowPanel when named panels exist. " +
                        "Either use only one unnamed panel, or name all panels.");
                }
                if (_defaultManager != null)
                {
                    throw new InvalidOperationException(
                        "Cannot register multiple unnamed WindowPanels. " +
                        "Either use only one unnamed panel, or name all panels.");
                }
                _defaultManager = manager;
            }
            else
            {
                // Registering a named panel
                if (_defaultManager != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot register named WindowPanel '{name}' when an unnamed panel exists. " +
                        "Either use only one unnamed panel, or name all panels.");
                }
                if (_managers.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        $"A WindowPanel named '{name}' is already registered.");
                }
                _managers[name] = manager;
            }
        }
    }

    /// <summary>
    /// Unregisters a WindowManager.
    /// </summary>
    /// <param name="manager">The window manager to unregister.</param>
    internal void Unregister(WindowManager manager)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_defaultManager, manager))
            {
                _defaultManager = null;
                return;
            }

            string? keyToRemove = null;
            foreach (var kvp in _managers)
            {
                if (ReferenceEquals(kvp.Value, manager))
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
            {
                _managers.Remove(keyToRemove);
            }
        }
    }

    /// <summary>
    /// Gets the default (unnamed) WindowManager.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no WindowPanel is registered, or if multiple named panels exist
    /// (use the indexer with a name instead).
    /// </exception>
    public WindowManager Default
    {
        get
        {
            lock (_lock)
            {
                if (_defaultManager != null)
                {
                    return _defaultManager;
                }

                if (_managers.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No WindowPanel found. Add a WindowPanel to your widget tree.");
                }

                if (_managers.Count == 1)
                {
                    // Allow accessing the single named panel as default
                    return _managers.Values.First();
                }

                throw new InvalidOperationException(
                    $"Multiple WindowPanels are registered ({string.Join(", ", _managers.Keys)}). " +
                    "Use Windows[\"name\"] to specify which panel to use.");
            }
        }
    }

    /// <summary>
    /// Gets a WindowManager by name.
    /// </summary>
    /// <param name="name">The name of the WindowPanel.</param>
    /// <exception cref="InvalidOperationException">Thrown if no panel with the given name exists.</exception>
    public WindowManager this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            
            lock (_lock)
            {
                if (_managers.TryGetValue(name, out var manager))
                {
                    return manager;
                }

                if (_defaultManager != null)
                {
                    throw new InvalidOperationException(
                        $"No WindowPanel named '{name}' found. " +
                        "The registered WindowPanel is unnamed. Use Windows.Default instead.");
                }

                if (_managers.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No WindowPanel named '{name}' found. No WindowPanels are registered.");
                }

                throw new InvalidOperationException(
                    $"No WindowPanel named '{name}' found. " +
                    $"Available panels: {string.Join(", ", _managers.Keys)}");
            }
        }
    }

    /// <summary>
    /// Tries to get the default WindowManager.
    /// </summary>
    /// <returns>The default manager, or null if not available.</returns>
    public WindowManager? TryGetDefault()
    {
        lock (_lock)
        {
            if (_defaultManager != null)
                return _defaultManager;
            
            if (_managers.Count == 1)
                return _managers.Values.First();
            
            return null;
        }
    }

    /// <summary>
    /// Tries to get a WindowManager by name.
    /// </summary>
    /// <param name="name">The name of the WindowPanel.</param>
    /// <returns>The manager, or null if not found.</returns>
    public WindowManager? TryGet(string name)
    {
        lock (_lock)
        {
            return _managers.TryGetValue(name, out var manager) ? manager : null;
        }
    }

    /// <summary>
    /// Gets whether any WindowPanels are registered.
    /// </summary>
    public bool HasPanels
    {
        get
        {
            lock (_lock)
            {
                return _defaultManager != null || _managers.Count > 0;
            }
        }
    }
}
