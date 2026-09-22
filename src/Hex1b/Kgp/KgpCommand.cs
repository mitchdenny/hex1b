namespace Hex1b;

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

    /// <summary>Usage-hint bitmask supplied by the client (N key).</summary>
    public uint UsageHints { get; init; }

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
    /// <exception cref="FormatException">
    /// <paramref name="controlData"/> contains malformed or invalid KGP control data.
    /// </exception>
    public static KgpCommand Parse(string controlData)
    {
        if (!KgpCommandParser.TryParse(controlData, out var command, out var failure))
        {
            throw new FormatException(
                $"Invalid KGP control data: {failure.FormatReason(controlData.AsSpan())}");
        }

        return FromParsed(command);
    }

    internal KgpParsedCommand.TransmissionData ToTransmissionData()
        => new(
            Format,
            Medium,
            Width,
            Height,
            FileSize,
            FileOffset,
            ImageId,
            ImageNumber,
            PlacementId,
            Compression == 'z'
                ? KgpParsedCommand.CompressionMode.Zlib
                : KgpParsedCommand.CompressionMode.None,
            MoreData != 0,
            UsageHints);

    internal static KgpCommand FromParsed(KgpParsedCommand command)
    {
        return command switch
        {
            KgpParsedCommand.Transmit transmit => CreateCompatibilityCommand(
                KgpAction.Transmit,
                transmit.Quiet,
                transmission: transmit.Transmission),
            KgpParsedCommand.TransmitAndDisplay transmitAndDisplay => CreateCompatibilityCommand(
                KgpAction.TransmitAndDisplay,
                transmitAndDisplay.Quiet,
                transmission: transmitAndDisplay.Transmission,
                display: transmitAndDisplay.Display),
            KgpParsedCommand.Query query => CreateCompatibilityCommand(
                KgpAction.Query,
                query.Quiet,
                transmission: query.Transmission),
            KgpParsedCommand.Put put => CreateCompatibilityCommand(
                KgpAction.Put,
                put.Quiet,
                display: put.Display),
            KgpParsedCommand.Delete delete => CreateCompatibilityCommand(
                KgpAction.Delete,
                delete.Quiet,
                delete: delete.Selector),
            KgpParsedCommand.AnimationFrame animationFrame => CreateCompatibilityCommand(
                KgpAction.AnimationFrame,
                animationFrame.Quiet,
                transmission: animationFrame.Transmission,
                animationFrame: animationFrame.Frame),
            KgpParsedCommand.AnimationControl animationControl => CreateCompatibilityCommand(
                KgpAction.AnimationControl,
                animationControl.Quiet,
                animationControl: animationControl.Control),
            KgpParsedCommand.Compose compose => CreateCompatibilityCommand(
                KgpAction.Compose,
                compose.Quiet,
                composition: compose.Composition),
            _ => throw new InvalidOperationException(
                $"Unsupported parsed KGP command type: {command.GetType().Name}."),
        };
    }

    private static KgpCommand CreateCompatibilityCommand(
        KgpAction action,
        KgpParsedCommand.QuietMode quiet,
        KgpParsedCommand.TransmissionData? transmission = null,
        KgpParsedCommand.DisplayData? display = null,
        KgpParsedCommand.DeleteSelector? delete = null,
        KgpParsedCommand.AnimationFrameData? animationFrame = null,
        KgpParsedCommand.AnimationControlData? animationControl = null,
        KgpParsedCommand.CompositionData? composition = null)
    {
        var transmissionData = transmission.GetValueOrDefault();
        var displayData = display.GetValueOrDefault();
        var deleteData = ProjectDelete(delete);
        var animationFrameData = animationFrame.GetValueOrDefault();
        var animationControlData = animationControl.GetValueOrDefault();
        var compositionData = composition.GetValueOrDefault();

        var imageId = transmission?.ImageId
            ?? display?.ImageId
            ?? deleteData.ImageId
            ?? animationControl?.ImageId
            ?? composition?.ImageId
            ?? 0;
        var imageNumber = transmission?.ImageNumber
            ?? display?.ImageNumber
            ?? deleteData.ImageNumber
            ?? animationControl?.ImageNumber
            ?? composition?.ImageNumber
            ?? 0;
        var placementId = transmission?.PlacementId
            ?? display?.PlacementId
            ?? deleteData.PlacementId
            ?? animationControl?.PlacementId
            ?? composition?.PlacementId
            ?? 0;

        return new KgpCommand
        {
            Action = action,
            Quiet = (int)quiet,
            Format = transmission?.Format ?? KgpFormat.Rgba32,
            Medium = transmission?.Medium ?? KgpTransmissionMedium.Direct,
            Width = transmissionData.Width,
            Height = transmissionData.Height,
            FileSize = transmissionData.FileSize,
            FileOffset = transmissionData.FileOffset,
            ImageId = imageId,
            ImageNumber = imageNumber,
            PlacementId = placementId,
            Compression = transmission?.Compression == KgpParsedCommand.CompressionMode.Zlib
                ? 'z'
                : null,
            MoreData = transmission?.MoreData == true ? 1 : 0,
            UsageHints = transmissionData.UsageHints,
            SourceX = display?.SourceX
                ?? deleteData.SourceX
                ?? animationFrame?.X
                ?? composition?.DestinationX
                ?? 0,
            SourceY = display?.SourceY
                ?? deleteData.SourceY
                ?? animationFrame?.Y
                ?? composition?.DestinationY
                ?? 0,
            SourceWidth = display?.SourceWidth
                ?? composition?.Width
                ?? 0,
            SourceHeight = display?.SourceHeight
                ?? composition?.Height
                ?? 0,
            CellOffsetX = display?.CellOffsetX
                ?? (animationFrame is not null
                    ? animationFrameData.Composition == KgpParsedCommand.CompositionMode.Overwrite
                        ? 1u
                        : 0u
                    : (uint?)null)
                ?? composition?.SourceX
                ?? 0,
            CellOffsetY = display?.CellOffsetY
                ?? animationFrame?.BackgroundColor
                ?? composition?.SourceY
                ?? 0,
            DisplayColumns = display?.Columns
                ?? animationFrame?.BaseFrameNumber
                ?? animationControl?.CurrentFrameNumber
                ?? composition?.DestinationFrameNumber
                ?? 0,
            DisplayRows = display?.Rows
                ?? deleteData.FrameNumber
                ?? animationFrame?.EditFrameNumber
                ?? animationControl?.AffectedFrameNumber
                ?? composition?.SourceFrameNumber
                ?? 0,
            CursorMovement = display is not null
                ? displayData.SuppressCursorMovement ? 1 : 0
                : composition is not null
                    ? compositionData.Composition == KgpParsedCommand.CompositionMode.Overwrite ? 1 : 0
                    : 0,
            UnicodePlaceholder = display?.UnicodePlaceholder == true ? 1 : 0,
            ZIndex = display?.ZIndex
                ?? deleteData.ZIndex
                ?? animationFrame?.Gap
                ?? animationControl?.Gap
                ?? 0,
            ParentImageId = displayData.ParentImageId,
            ParentPlacementId = displayData.ParentPlacementId,
            ParentOffsetH = displayData.ParentOffsetHorizontal,
            ParentOffsetV = displayData.ParentOffsetVertical,
            DeleteTarget = delete?.Target ?? KgpDeleteTarget.All,
            AnimationState = animationControl is null
                ? 0
                : animationControlData.State switch
                {
                    KgpParsedCommand.AnimationPlaybackState.Stopped => 1,
                    KgpParsedCommand.AnimationPlaybackState.Loading => 2,
                    KgpParsedCommand.AnimationPlaybackState.Running => 3,
                    _ => 0,
                },
            LoopCount = animationControlData.LoopCount,
        };
    }

    private static (
        uint? ImageId,
        uint? ImageNumber,
        uint? PlacementId,
        uint? SourceX,
        uint? SourceY,
        int? ZIndex,
        uint? FrameNumber) ProjectDelete(KgpParsedCommand.DeleteSelector? delete)
    {
        return delete switch
        {
            KgpParsedCommand.DeleteSelector.ById byId
                => (byId.ImageId, null, byId.PlacementId, null, null, null, null),
            KgpParsedCommand.DeleteSelector.ByNumber byNumber
                => (null, byNumber.ImageNumber, byNumber.PlacementId, null, null, null, null),
            KgpParsedCommand.DeleteSelector.AnimationFrames frames
                => (frames.ImageId, frames.ImageNumber, null, null, null, null, frames.FrameNumber),
            KgpParsedCommand.DeleteSelector.AtCell atCell
                => (null, null, null, atCell.X, atCell.Y, null, null),
            KgpParsedCommand.DeleteSelector.AtCellWithZIndex atCellWithZ
                => (null, null, null, atCellWithZ.X, atCellWithZ.Y, atCellWithZ.ZIndex, null),
            KgpParsedCommand.DeleteSelector.ByRange range
                => (null, null, null, range.FirstImageId, range.LastImageId, null, null),
            KgpParsedCommand.DeleteSelector.ByColumn column
                => (null, null, null, column.Column, null, null, null),
            KgpParsedCommand.DeleteSelector.ByRow row
                => (null, null, null, null, row.Row, null, null),
            KgpParsedCommand.DeleteSelector.ByZIndex zIndex
                => (null, null, null, null, null, zIndex.ZIndex, null),
            _ => (null, null, null, null, null, null, null),
        };
    }
}
