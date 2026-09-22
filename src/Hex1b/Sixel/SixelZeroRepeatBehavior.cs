using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies how a zero DECGRI repeat count is interpreted.
/// </summary>
internal enum SixelZeroRepeatBehavior
{
    /// <summary>Treat zero as the DEC default of one repetition.</summary>
    RepeatOnce,

    /// <summary>Treat zero as no repetitions.</summary>
    RepeatZeroTimes,
}
