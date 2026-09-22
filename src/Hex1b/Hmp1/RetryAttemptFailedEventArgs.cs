using System.Runtime.CompilerServices;

namespace Hex1b;

/// <summary>
/// Carries information about a single failed retry attempt.
/// </summary>
public sealed record RetryAttemptFailedEventArgs(int AttemptNumber, TimeSpan NextDelay, Exception Error);
