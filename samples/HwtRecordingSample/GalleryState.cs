using Hex1b.Surfaces;

namespace HwtRecordingSample;

internal sealed class GalleryState
{
    private readonly byte[][] _landscapes =
        [GalleryArtwork.Landscape(night: false), GalleryArtwork.Landscape(night: true)];
    private readonly SixelPixelBuffer[] _waves =
        [GalleryArtwork.Waves(night: false), GalleryArtwork.Waves(night: true)];

    public int Count { get; private set; }
    public string SceneName => Count % 2 == 0 ? "Daybreak" : "Moonrise";
    public byte[] Landscape => _landscapes[Count % 2];
    public SixelPixelBuffer Waves => _waves[Count % 2];

    public void Next() => Count++;
}
