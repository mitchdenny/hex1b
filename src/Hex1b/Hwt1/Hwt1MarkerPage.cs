namespace Hex1b;

// Pages share one immutable history snapshot and are published in frame-revision order.
internal sealed record Hwt1MarkerPage(string Revision, int Offset, int Total);
