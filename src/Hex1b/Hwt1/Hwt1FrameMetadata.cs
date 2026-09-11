namespace Hex1b;

internal sealed record Hwt1FrameMetadata(
    int Version, uint Revision, uint BaseRevision, bool Full,
    int Columns, int Rows, int CellWidth, int CellHeight, int MouseTracking,
    uint DefaultBackground, uint DefaultForeground, Hwt1Cursor Cursor,
    List<Hwt1RenderImage> Images, string[] RetainedImages, List<Hwt1RenderPlacement> Placements,
    Hwt1FrameStatistics Stats, List<string> Warnings, Hwt1Peer Peer, Hwt1History? History,
    List<Hwt1Hyperlink> Hyperlinks, string Title,
    Hwt1Progress Progress, Hwt1ShellIntegration ShellIntegration);
