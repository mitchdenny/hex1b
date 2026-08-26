namespace Hex1b;

internal abstract record KgpParsedCommand(
    KgpParsedCommand.QuietMode Quiet,
    KgpControlKeySet ControlKeys)
{
    internal bool TryGetTransmission(out TransmissionData transmission)
    {
        switch (this)
        {
            case Transmit transmit:
                transmission = transmit.Transmission;
                return true;
            case TransmitAndDisplay transmitAndDisplay:
                transmission = transmitAndDisplay.Transmission;
                return true;
            case Query query:
                transmission = query.Transmission;
                return true;
            case AnimationFrame animationFrame:
                transmission = animationFrame.Transmission;
                return true;
            default:
                transmission = default;
                return false;
        }
    }

    internal enum QuietMode
    {
        Normal,
        SuppressSuccess,
        SuppressAll,
    }

    internal enum ImageIdentityKind
    {
        Anonymous,
        ExplicitId,
        Number,
    }

    internal enum CompressionMode
    {
        None,
        Zlib,
    }

    internal enum CompositionMode
    {
        AlphaBlend,
        Overwrite,
    }

    internal enum AnimationPlaybackState
    {
        None,
        Stopped,
        Loading,
        Running,
    }

    internal sealed record Transmit(
        TransmissionData Transmission,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record TransmitAndDisplay(
        TransmissionData Transmission,
        DisplayData Display,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record Query(
        TransmissionData Transmission,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record Put(
        DisplayData Display,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record Delete(
        DeleteSelector Selector,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record AnimationFrame(
        TransmissionData Transmission,
        AnimationFrameData Frame,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record AnimationControl(
        AnimationControlData Control,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal sealed record Compose(
        CompositionData Composition,
        QuietMode Quiet,
        KgpControlKeySet ControlKeys) : KgpParsedCommand(Quiet, ControlKeys);

    internal readonly record struct TransmissionData(
        KgpFormat Format,
        KgpTransmissionMedium Medium,
        uint Width,
        uint Height,
        uint FileSize,
        uint FileOffset,
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        CompressionMode Compression,
        bool MoreData,
        uint UsageHints)
    {
        internal ImageIdentityKind IdentityKind
            => ImageId > 0
                ? ImageIdentityKind.ExplicitId
                : ImageNumber > 0
                    ? ImageIdentityKind.Number
                    : ImageIdentityKind.Anonymous;
    }

    internal readonly record struct DisplayData(
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        uint SourceX,
        uint SourceY,
        uint SourceWidth,
        uint SourceHeight,
        uint CellOffsetX,
        uint CellOffsetY,
        uint Columns,
        uint Rows,
        bool SuppressCursorMovement,
        bool UnicodePlaceholder,
        int ZIndex,
        uint ParentImageId,
        uint ParentPlacementId,
        int ParentOffsetHorizontal,
        int ParentOffsetVertical);

    internal readonly record struct AnimationFrameData(
        uint X,
        uint Y,
        uint BaseFrameNumber,
        uint EditFrameNumber,
        int Gap,
        CompositionMode Composition,
        uint BackgroundColor);

    internal readonly record struct AnimationControlData(
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        AnimationPlaybackState State,
        uint LoopCount,
        uint CurrentFrameNumber,
        uint AffectedFrameNumber,
        int Gap);

    internal readonly record struct CompositionData(
        uint ImageId,
        uint ImageNumber,
        uint PlacementId,
        uint DestinationFrameNumber,
        uint SourceFrameNumber,
        uint DestinationX,
        uint DestinationY,
        uint Width,
        uint Height,
        uint SourceX,
        uint SourceY,
        CompositionMode Composition);

    internal abstract record DeleteSelector(bool DeleteImageData)
    {
        internal abstract KgpDeleteTarget Target { get; }

        internal sealed record All(bool FreeData) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.AllFreeData : KgpDeleteTarget.All;
        }

        internal sealed record ById(
            bool FreeData,
            uint ImageId,
            uint PlacementId) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByIdFreeData : KgpDeleteTarget.ById;
        }

        internal sealed record ByNumber(
            bool FreeData,
            uint ImageNumber,
            uint PlacementId) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByNumberFreeData : KgpDeleteTarget.ByNumber;
        }

        internal sealed record AtCursor(bool FreeData) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.AtCursorFreeData : KgpDeleteTarget.AtCursor;
        }

        internal sealed record AnimationFrames(
            bool FreeData,
            uint ImageId,
            uint ImageNumber,
            uint FrameNumber) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData
                    ? KgpDeleteTarget.AnimationFramesFreeData
                    : KgpDeleteTarget.AnimationFrames;
        }

        internal sealed record AtCell(
            bool FreeData,
            uint X,
            uint Y) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.AtCellFreeData : KgpDeleteTarget.AtCell;
        }

        internal sealed record AtCellWithZIndex(
            bool FreeData,
            uint X,
            uint Y,
            int ZIndex) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData
                    ? KgpDeleteTarget.AtCellWithZIndexFreeData
                    : KgpDeleteTarget.AtCellWithZIndex;
        }

        internal sealed record ByRange(
            bool FreeData,
            uint FirstImageId,
            uint LastImageId) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByRangeFreeData : KgpDeleteTarget.ByRange;
        }

        internal sealed record ByColumn(
            bool FreeData,
            uint Column) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByColumnFreeData : KgpDeleteTarget.ByColumn;
        }

        internal sealed record ByRow(
            bool FreeData,
            uint Row) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByRowFreeData : KgpDeleteTarget.ByRow;
        }

        internal sealed record ByZIndex(
            bool FreeData,
            int ZIndex) : DeleteSelector(FreeData)
        {
            internal override KgpDeleteTarget Target
                => FreeData ? KgpDeleteTarget.ByZIndexFreeData : KgpDeleteTarget.ByZIndex;
        }
    }
}
