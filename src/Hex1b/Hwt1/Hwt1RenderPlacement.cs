namespace Hex1b;

internal sealed record Hwt1RenderPlacement(
    string Key, string Kind, double X, double Y, double Width, double Height,
    double SourceX, double SourceY, double SourceWidth, double SourceHeight,
    double ClipX, double ClipY, double ClipWidth, double ClipHeight, int Z);
