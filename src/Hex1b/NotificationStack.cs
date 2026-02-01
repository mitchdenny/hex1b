namespace Hex1b;

/// <summary>
/// Manages a collection of notifications with floating/docked state tracking.
/// </summary>
/// <remarks>
/// <para>
/// The notification stack tracks:
/// <list type="bullet">
///   <item><description>All active notifications</description></item>
///   <item><description>Which notifications are currently "floating" (visible as overlays)</description></item>
///   <item><description>Timeout timers for auto-hiding from floating view</description></item>
/// </list>
/// </para>
/// <para>
/// Notifications have three states:
/// <list type="number">
///   <item><description>Floating - visible as overlay, not yet timed out</description></item>
///   <item><description>Docked - timed out but still in the panel</description></item>
///   <item><description>Dismissed - removed from the stack entirely</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class NotificationStack
{
    private readonly List<NotificationEntry> _entries = new();
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when the notification collection changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Whether the notification panel/drawer is visible.
    /// </summary>
    public bool IsPanelVisible { get; set; }

    /// <summary>
    /// Shows the notification panel/drawer.
    /// </summary>
    public void ShowPanel()
    {
        if (!IsPanelVisible)
        {
            IsPanelVisible = true;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Hides the notification panel/drawer.
    /// </summary>
    public void HidePanel()
    {
        if (IsPanelVisible)
        {
            IsPanelVisible = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Toggles the notification panel/drawer visibility.
    /// </summary>
    public void TogglePanel()
    {
        IsPanelVisible = !IsPanelVisible;
        Changed?.Invoke();
    }

    /// <summary>
    /// Gets all active notifications in the stack (both floating and docked).
    /// Newest notifications are first.
    /// </summary>
    public IReadOnlyList<Notification> All
    {
        get
        {
            lock (_lock)
            {
                return _entries.Select(e => e.Notification).ToList();
            }
        }
    }

    /// <summary>
    /// Gets notifications that are currently floating (visible as overlays).
    /// Newest notifications are first.
    /// </summary>
    public IReadOnlyList<Notification> Floating
    {
        get
        {
            lock (_lock)
            {
                return _entries
                    .Where(e => e.IsFloating)
                    .Select(e => e.Notification)
                    .ToList();
            }
        }
    }

    /// <summary>
    /// Gets the count of all notifications.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Gets the count of floating notifications.
    /// </summary>
    public int FloatingCount
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count(e => e.IsFloating);
            }
        }
    }

    /// <summary>
    /// Posts a new notification to the stack.
    /// The notification will appear as a floating overlay.
    /// </summary>
    /// <param name="notification">The notification to post.</param>
    public void Post(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        lock (_lock)
        {
            // Check if already posted
            if (_entries.Any(e => ReferenceEquals(e.Notification, notification)))
            {
                return;
            }

            var entry = new NotificationEntry(notification);
            _entries.Insert(0, entry); // Newest first

            // Start timeout timer if configured
            if (notification.Timeout.HasValue)
            {
                StartTimeoutTimer(entry, notification.Timeout.Value);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Dismisses a notification, removing it from the stack entirely.
    /// </summary>
    /// <param name="notification">The notification to dismiss.</param>
    /// <returns>True if the notification was found and removed.</returns>
    public bool Dismiss(Notification notification)
    {
        NotificationEntry? entry;

        lock (_lock)
        {
            entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Notification, notification));
            if (entry == null)
            {
                return false;
            }

            entry.CancelTimeout();
            _entries.Remove(entry);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Dismisses all notifications from the stack.
    /// </summary>
    public void DismissAll()
    {
        lock (_lock)
        {
            foreach (var entry in _entries)
            {
                entry.CancelTimeout();
            }
            _entries.Clear();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Checks if a notification is currently floating.
    /// </summary>
    /// <param name="notification">The notification to check.</param>
    /// <returns>True if the notification is floating.</returns>
    public bool IsFloating(Notification notification)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Notification, notification));
            return entry?.IsFloating ?? false;
        }
    }

    /// <summary>
    /// Hides a notification from floating view without dismissing it.
    /// The notification remains in the panel.
    /// </summary>
    /// <param name="notification">The notification to hide.</param>
    public void HideFromFloating(Notification notification)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Notification, notification));
            if (entry != null)
            {
                entry.IsFloating = false;
            }
        }

        Changed?.Invoke();
    }

    private void StartTimeoutTimer(NotificationEntry entry, TimeSpan timeout)
    {
        var cts = new CancellationTokenSource();
        entry.TimeoutCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeout, cts.Token);

                lock (_lock)
                {
                    if (_entries.Contains(entry))
                    {
                        entry.IsFloating = false;
                    }
                }

                // Invoke timeout handler outside lock
                if (entry.Notification.TimeoutHandler != null)
                {
                    var context = new NotificationEventContext(
                        entry.Notification,
                        this,
                        CancellationToken.None);
                    
                    try
                    {
                        await entry.Notification.TimeoutHandler(context);
                    }
                    catch
                    {
                        // Swallow handler exceptions
                    }
                }

                Changed?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled (notification dismissed before timeout)
            }
        });
    }

    private sealed class NotificationEntry
    {
        public NotificationEntry(Notification notification)
        {
            Notification = notification;
            IsFloating = true;
        }

        public Notification Notification { get; }
        public bool IsFloating { get; set; }
        public CancellationTokenSource? TimeoutCts { get; set; }

        public void CancelTimeout()
        {
            TimeoutCts?.Cancel();
            TimeoutCts?.Dispose();
            TimeoutCts = null;
        }
    }
}
