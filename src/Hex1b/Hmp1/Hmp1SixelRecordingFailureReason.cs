namespace Hex1b;

/// <summary>
/// Explicit failure reasons for <see cref="Hmp1SixelRecording"/> serialization and
/// deserialization.
/// </summary>
internal enum Hmp1SixelRecordingFailureReason
{
    /// <summary>The recording's format marker was not recognized.</summary>
    Malformed,

    /// <summary>The recording ended before all declared data could be read.</summary>
    Truncated,

    /// <summary>The recording's version is not supported by this build.</summary>
    UnsupportedVersion,

    /// <summary>A placement referenced an image table index that does not exist.</summary>
    MissingImageReference,

    /// <summary>A declared extent or cell dimension was not positive.</summary>
    InvalidGeometry,

    /// <summary>A placement, image, or payload count/size exceeded a bounded limit.</summary>
    ResourceLimitExceeded,
}
