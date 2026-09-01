using Hex1b;
using SkiaSharp;

static class GifDecoder
{
    private const int MaximumWidth = 160;
    private const int MaximumHeight = 120;
    private const int MaximumFrames = 60;

    internal static (byte[] Data, int Width, int Height)? DecodeFirstFrame(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount < 2)
            return null;

        var imageInfo = CreateImageInfo(codec.Info);
        using var bitmap = new SKBitmap(imageInfo);
        var result = codec.GetPixels(imageInfo, bitmap.GetPixels(), new SKCodecOptions(0));
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            return null;

        return (CopyPixels(bitmap), imageInfo.Width, imageInfo.Height);
    }

    internal static IReadOnlyList<KgpAnimationFrame>? DecodeAnimation(
        byte[] encoded,
        int width,
        int height)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount < 2)
            return null;

        var imageInfo = new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        var frameInfo = codec.FrameInfo;
        var frames = new List<KgpAnimationFrame>(Math.Min(codec.FrameCount, MaximumFrames));

        for (var index = 0; index < codec.FrameCount && index < MaximumFrames; index++)
        {
            var priorFrame = index == 0 ? -1 : index - 1;
            var options = new SKCodecOptions(index, priorFrame);
            var result = codec.GetPixels(imageInfo, bitmap.GetPixels(), options);
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                return null;

            frames.Add(new KgpAnimationFrame(
                CopyPixels(bitmap),
                Math.Max(20, frameInfo[index].Duration)));
        }

        return frames;
    }

    private static SKImageInfo CreateImageInfo(SKImageInfo source)
    {
        var scale = Math.Min(
            1.0,
            Math.Min((double)MaximumWidth / source.Width, (double)MaximumHeight / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul);
    }

    private static byte[] CopyPixels(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        var rgba = new byte[pixels.Length];
        pixels.CopyTo(rgba);
        return rgba;
    }
}
