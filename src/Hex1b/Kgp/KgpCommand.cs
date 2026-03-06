namespace Hex1b;

/// <summary>
/// The pixel data format for KGP image transmission.
/// Specified by the 'f' key in the control data.
/// </summary>
public enum KgpFormat
{
    /// <summary>24-bit RGB data, 3 bytes per pixel (f=24).</summary>
    Rgb24 = 24,

    /// <summary>32-bit RGBA data, 4 bytes per pixel (f=32, default).</summary>
    Rgba32 = 32,

    /// <summary>PNG image data (f=100).</summary>
    Png = 100,
}

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

/// <summary>
/// The deletion target specifier for KGP delete commands.
/// Specified by the 'd' key in the control data.
/// </summary>
public enum KgpDeleteTarget
{
    /// <summary>Delete all visible placements (d=a).</summary>
    All,

    /// <summary>Delete all visible placements, free data (d=A).</summary>
    AllFreeData,

    /// <summary>Delete by image ID (d=i).</summary>
    ById,

    /// <summary>Delete by image ID, free data (d=I).</summary>
    ByIdFreeData,

    /// <summary>Delete newest by image number (d=n).</summary>
    ByNumber,

    /// <summary>Delete newest by image number, free data (d=N).</summary>
    ByNumberFreeData,

    /// <summary>Delete at cursor position (d=c).</summary>
    AtCursor,

    /// <summary>Delete at cursor position, free data (d=C).</summary>
    AtCursorFreeData,

    /// <summary>Delete at specific cell (d=p).</summary>
    AtCell,

    /// <summary>Delete at specific cell, free data (d=P).</summary>
    AtCellFreeData,

    /// <summary>Delete at cell with z-index (d=q).</summary>
    AtCellWithZIndex,

    /// <summary>Delete at cell with z-index, free data (d=Q).</summary>
    AtCellWithZIndexFreeData,

    /// <summary>Delete by column (d=x).</summary>
    ByColumn,

    /// <summary>Delete by column, free data (d=X).</summary>
    ByColumnFreeData,

    /// <summary>Delete by row (d=y).</summary>
    ByRow,

    /// <summary>Delete by row, free data (d=Y).</summary>
    ByRowFreeData,

    /// <summary>Delete by z-index (d=z).</summary>
    ByZIndex,

    /// <summary>Delete by z-index, free data (d=Z).</summary>
    ByZIndexFreeData,

    /// <summary>Delete by ID range (d=r).</summary>
    ByRange,

    /// <summary>Delete by ID range, free data (d=R).</summary>
    ByRangeFreeData,

    /// <summary>Delete animation frames (d=f).</summary>
    AnimationFrames,

    /// <summary>Delete animation frames, free data (d=F).</summary>
    AnimationFramesFreeData,
}

/// <summary>
/// Parsed KGP command from control data key=value pairs.
/// </summary>
/// <remarks>
/// Protocol specification: https://sw.kovidgoyal.net/kitty/graphics-protocol/
/// </remarks>
public sealed class KgpCommand
{
    /// <summary>The overall action (a key). Default: Transmit.</summary>
    public KgpAction Action { get; init; } = KgpAction.Transmit;

    /// <summary>Response suppression (q key). 0=normal, 1=suppress OK, 2=suppress all.</summary>
    public int Quiet { get; init; }

    // --- Transmission keys ---

    /// <summary>Pixel data format (f key). Default: Rgba32.</summary>
    public KgpFormat Format { get; init; } = KgpFormat.Rgba32;

    /// <summary>Transmission medium (t key). Default: Direct.</summary>
    public KgpTransmissionMedium Medium { get; init; } = KgpTransmissionMedium.Direct;

    /// <summary>Image width in pixels (s key).</summary>
    public uint Width { get; init; }

    /// <summary>Image height in pixels (v key).</summary>
    public uint Height { get; init; }

    /// <summary>Size of data to read from file (S key).</summary>
    public uint FileSize { get; init; }

    /// <summary>Offset to read from file (O key).</summary>
    public uint FileOffset { get; init; }

    /// <summary>Image ID (i key). 0 means unspecified.</summary>
    public uint ImageId { get; init; }

    /// <summary>Image number (I key). 0 means unspecified.</summary>
    public uint ImageNumber { get; init; }

    /// <summary>Placement ID (p key). 0 means unspecified.</summary>
    public uint PlacementId { get; init; }

    /// <summary>Compression type (o key). 'z' for zlib, null for none.</summary>
    public char? Compression { get; init; }

    /// <summary>Whether more chunked data follows (m key). 0=last/only, 1=more.</summary>
    public int MoreData { get; init; }

    // --- Display keys ---

    /// <summary>Left edge of source rectangle in pixels (x key).</summary>
    public uint SourceX { get; init; }

    /// <summary>Top edge of source rectangle in pixels (y key).</summary>
    public uint SourceY { get; init; }

    /// <summary>Width of source rectangle in pixels (w key). 0=entire width.</summary>
    public uint SourceWidth { get; init; }

    /// <summary>Height of source rectangle in pixels (h key). 0=entire height.</summary>
    public uint SourceHeight { get; init; }

    /// <summary>X-offset within first cell in pixels (X key).</summary>
    public uint CellOffsetX { get; init; }

    /// <summary>Y-offset within first cell in pixels (Y key).</summary>
    public uint CellOffsetY { get; init; }

    /// <summary>Number of columns to display over (c key). 0=auto.</summary>
    public uint DisplayColumns { get; init; }

    /// <summary>Number of rows to display over (r key). 0=auto.</summary>
    public uint DisplayRows { get; init; }

    /// <summary>Cursor movement policy (C key). 0=move cursor, 1=don't move.</summary>
    public int CursorMovement { get; init; }

    /// <summary>Unicode placeholder mode (U key). 1=create virtual placement.</summary>
    public int UnicodePlaceholder { get; init; }

    /// <summary>Z-index for stacking order (z key).</summary>
    public int ZIndex { get; init; }

    /// <summary>Parent image ID for relative placement (P key).</summary>
    public uint ParentImageId { get; init; }

    /// <summary>Parent placement ID for relative placement (Q key).</summary>
    public uint ParentPlacementId { get; init; }

    /// <summary>Horizontal offset from parent in cells (H key).</summary>
    public int ParentOffsetH { get; init; }

    /// <summary>Vertical offset from parent in cells (V key).</summary>
    public int ParentOffsetV { get; init; }

    // --- Delete keys ---

    /// <summary>Deletion target (d key). Default: All.</summary>
    public KgpDeleteTarget DeleteTarget { get; init; } = KgpDeleteTarget.All;

    // --- Animation keys (used with a=f, a=a, a=c) ---

    /// <summary>Animation state (s key for a=a). 1=stop, 2=loading, 3=run.</summary>
    public int AnimationState { get; init; }

    /// <summary>Loop count (v key for a=a). 0=ignored, 1=infinite, n=n-1 loops.</summary>
    public uint LoopCount { get; init; }

    /// <summary>
    /// Parses a KGP control data string into a <see cref="KgpCommand"/>.
    /// </summary>
    /// <param name="controlData">Comma-separated key=value pairs (e.g., "a=T,f=24,s=10,v=20,i=1").</param>
    /// <returns>A parsed <see cref="KgpCommand"/> with defaults for unspecified keys.</returns>
    public static KgpCommand Parse(string controlData)
    {
        if (string.IsNullOrEmpty(controlData))
            return new KgpCommand();

        var cmd = new KgpCommand();
        var action = KgpAction.Transmit;
        var format = KgpFormat.Rgba32;
        var medium = KgpTransmissionMedium.Direct;
        var deleteTarget = KgpDeleteTarget.All;
        int quiet = 0;
        uint width = 0, height = 0, fileSize = 0, fileOffset = 0;
        uint imageId = 0, imageNumber = 0, placementId = 0;
        char? compression = null;
        int moreData = 0;
        uint sourceX = 0, sourceY = 0, sourceWidth = 0, sourceHeight = 0;
        uint cellOffsetX = 0, cellOffsetY = 0;
        uint displayColumns = 0, displayRows = 0;
        int cursorMovement = 0, unicodePlaceholder = 0, zIndex = 0;
        uint parentImageId = 0, parentPlacementId = 0;
        int parentOffsetH = 0, parentOffsetV = 0;
        int animationState = 0;
        uint loopCount = 0;

        foreach (var pair in controlData.Split(','))
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex < 1 || eqIndex >= pair.Length - 1)
                continue;

            var key = pair[..eqIndex];
            var value = pair[(eqIndex + 1)..];

            switch (key)
            {
                case "a":
                    action = ParseAction(value);
                    break;
                case "q":
                    _ = int.TryParse(value, out quiet);
                    break;
                case "f":
                    format = ParseFormat(value);
                    break;
                case "t":
                    medium = ParseMedium(value);
                    break;
                case "s":
                    _ = uint.TryParse(value, out width);
                    break;
                case "v":
                    _ = uint.TryParse(value, out height);
                    break;
                case "S":
                    _ = uint.TryParse(value, out fileSize);
                    break;
                case "O":
                    _ = uint.TryParse(value, out fileOffset);
                    break;
                case "i":
                    _ = uint.TryParse(value, out imageId);
                    break;
                case "I":
                    _ = uint.TryParse(value, out imageNumber);
                    break;
                case "p":
                    _ = uint.TryParse(value, out placementId);
                    break;
                case "o":
                    compression = value.Length > 0 ? value[0] : null;
                    break;
                case "m":
                    _ = int.TryParse(value, out moreData);
                    break;
                case "x":
                    _ = uint.TryParse(value, out sourceX);
                    break;
                case "y":
                    _ = uint.TryParse(value, out sourceY);
                    break;
                case "w":
                    _ = uint.TryParse(value, out sourceWidth);
                    break;
                case "h":
                    _ = uint.TryParse(value, out sourceHeight);
                    break;
                case "X":
                    _ = uint.TryParse(value, out cellOffsetX);
                    break;
                case "Y":
                    _ = uint.TryParse(value, out cellOffsetY);
                    break;
                case "c":
                    _ = uint.TryParse(value, out displayColumns);
                    break;
                case "r":
                    _ = uint.TryParse(value, out displayRows);
                    break;
                case "C":
                    _ = int.TryParse(value, out cursorMovement);
                    break;
                case "U":
                    _ = int.TryParse(value, out unicodePlaceholder);
                    break;
                case "z":
                    _ = int.TryParse(value, out zIndex);
                    break;
                case "P":
                    _ = uint.TryParse(value, out parentImageId);
                    break;
                case "Q":
                    _ = uint.TryParse(value, out parentPlacementId);
                    break;
                case "H":
                    _ = int.TryParse(value, out parentOffsetH);
                    break;
                case "V":
                    _ = int.TryParse(value, out parentOffsetV);
                    break;
                case "d":
                    deleteTarget = ParseDeleteTarget(value);
                    break;
            }
        }

        return new KgpCommand
        {
            Action = action,
            Quiet = quiet,
            Format = format,
            Medium = medium,
            Width = width,
            Height = height,
            FileSize = fileSize,
            FileOffset = fileOffset,
            ImageId = imageId,
            ImageNumber = imageNumber,
            PlacementId = placementId,
            Compression = compression,
            MoreData = moreData,
            SourceX = sourceX,
            SourceY = sourceY,
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight,
            CellOffsetX = cellOffsetX,
            CellOffsetY = cellOffsetY,
            DisplayColumns = displayColumns,
            DisplayRows = displayRows,
            CursorMovement = cursorMovement,
            UnicodePlaceholder = unicodePlaceholder,
            ZIndex = zIndex,
            ParentImageId = parentImageId,
            ParentPlacementId = parentPlacementId,
            ParentOffsetH = parentOffsetH,
            ParentOffsetV = parentOffsetV,
            DeleteTarget = deleteTarget,
            AnimationState = animationState,
            LoopCount = loopCount,
        };
    }

    private static KgpAction ParseAction(string value)
    {
        if (value.Length != 1)
            return KgpAction.Transmit;

        return value[0] switch
        {
            't' => KgpAction.Transmit,
            'T' => KgpAction.TransmitAndDisplay,
            'q' => KgpAction.Query,
            'p' => KgpAction.Put,
            'd' => KgpAction.Delete,
            'f' => KgpAction.AnimationFrame,
            'a' => KgpAction.AnimationControl,
            'c' => KgpAction.Compose,
            _ => KgpAction.Transmit,
        };
    }

    private static KgpFormat ParseFormat(string value)
    {
        return value switch
        {
            "24" => KgpFormat.Rgb24,
            "32" => KgpFormat.Rgba32,
            "100" => KgpFormat.Png,
            _ => KgpFormat.Rgba32,
        };
    }

    private static KgpTransmissionMedium ParseMedium(string value)
    {
        if (value.Length != 1)
            return KgpTransmissionMedium.Direct;

        return value[0] switch
        {
            'd' => KgpTransmissionMedium.Direct,
            'f' => KgpTransmissionMedium.File,
            't' => KgpTransmissionMedium.TempFile,
            's' => KgpTransmissionMedium.SharedMemory,
            _ => KgpTransmissionMedium.Direct,
        };
    }

    private static KgpDeleteTarget ParseDeleteTarget(string value)
    {
        if (value.Length != 1)
            return KgpDeleteTarget.All;

        return value[0] switch
        {
            'a' => KgpDeleteTarget.All,
            'A' => KgpDeleteTarget.AllFreeData,
            'i' => KgpDeleteTarget.ById,
            'I' => KgpDeleteTarget.ByIdFreeData,
            'n' => KgpDeleteTarget.ByNumber,
            'N' => KgpDeleteTarget.ByNumberFreeData,
            'c' => KgpDeleteTarget.AtCursor,
            'C' => KgpDeleteTarget.AtCursorFreeData,
            'p' => KgpDeleteTarget.AtCell,
            'P' => KgpDeleteTarget.AtCellFreeData,
            'q' => KgpDeleteTarget.AtCellWithZIndex,
            'Q' => KgpDeleteTarget.AtCellWithZIndexFreeData,
            'x' => KgpDeleteTarget.ByColumn,
            'X' => KgpDeleteTarget.ByColumnFreeData,
            'y' => KgpDeleteTarget.ByRow,
            'Y' => KgpDeleteTarget.ByRowFreeData,
            'z' => KgpDeleteTarget.ByZIndex,
            'Z' => KgpDeleteTarget.ByZIndexFreeData,
            'r' => KgpDeleteTarget.ByRange,
            'R' => KgpDeleteTarget.ByRangeFreeData,
            'f' => KgpDeleteTarget.AnimationFrames,
            'F' => KgpDeleteTarget.AnimationFramesFreeData,
            _ => KgpDeleteTarget.All,
        };
    }
}
