namespace Hex1b;

/// <summary>
/// The transmission medium for KGP image data.
/// Specified by the 't' key in the control data.
/// </summary>
public enum KgpTransmissionMedium
{
    /// <summary>Direct: data is sent inline in the escape code (t=d, default).</summary>
    Direct,

    /// <summary>Regular file path (t=f).</summary>
    File,

    /// <summary>Temporary file, deleted after reading (t=t).</summary>
    TempFile,

    /// <summary>POSIX shared memory object (t=s).</summary>
    SharedMemory,
}
