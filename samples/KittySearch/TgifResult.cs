using Hex1b;

sealed class TgifResult
{
    private readonly byte[] _encodedGif;
    private readonly object _animationGate = new();
    private IReadOnlyList<KgpAnimationFrame>? _animationFrames;
    private byte[]? _whiteFrame;
    private string? _nativePlaybackCommand;
    private string? _clientDrivenPlaybackCommand;

    internal TgifResult(
        string description,
        string sourceUrl,
        byte[] encodedGif,
        byte[] previewData,
        int pixelWidth,
        int pixelHeight)
    {
        Description = description;
        SourceUrl = sourceUrl;
        _encodedGif = encodedGif;
        PreviewData = previewData;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
    }

    internal string Description { get; }

    internal string SourceUrl { get; }

    internal byte[] PreviewData { get; }

    internal int PixelWidth { get; }

    internal int PixelHeight { get; }

    internal IReadOnlyList<KgpAnimationFrame> GetAnimationFrames()
    {
        lock (_animationGate)
        {
            return _animationFrames ??= GifDecoder.DecodeAnimation(
                    _encodedGif,
                    PixelWidth,
                    PixelHeight)
                ?? throw new InvalidDataException("The TGIF animation could not be decoded.");
        }
    }

    internal byte[] GetWhiteFrame()
    {
        lock (_animationGate)
        {
            if (_whiteFrame is not null)
                return _whiteFrame;

            _whiteFrame = new byte[checked(PixelWidth * PixelHeight * 4)];
            Array.Fill(_whiteFrame, byte.MaxValue);
            return _whiteFrame;
        }
    }

    internal string GetPlaybackCommand(bool supportsNativeAnimation)
    {
        lock (_animationGate)
        {
            ref var cached = ref supportsNativeAnimation
                ? ref _nativePlaybackCommand
                : ref _clientDrivenPlaybackCommand;
            return cached ??= KgpPlaybackCommandBuilder.Build(
                GetAnimationFrames(),
                PixelWidth,
                PixelHeight,
                supportsNativeAnimation);
        }
    }
}
