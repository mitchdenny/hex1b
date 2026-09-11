namespace Hex1b.Sixel;

internal static class SixelPayload
{
    private const string SevenBitIntroducer = "\x1bP";
    private const string SevenBitTerminator = "\x1b\\";
    private const char EightBitIntroducer = '\x90';
    private const char EightBitTerminator = '\x9c';

    public static string NormalizeAndValidate(string imageData, string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(imageData, parameterName);

        string framed;
        if (imageData.StartsWith(SevenBitIntroducer, StringComparison.Ordinal))
        {
            if (!imageData.EndsWith(SevenBitTerminator, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A framed Sixel sequence must end with the ESC \\ string terminator.",
                    parameterName);
            }

            framed = imageData;
        }
        else if (imageData[0] == EightBitIntroducer)
        {
            if (imageData[^1] != EightBitTerminator)
            {
                throw new ArgumentException(
                    "An 8-bit framed Sixel sequence must end with the 8-bit string terminator.",
                    parameterName);
            }

            framed = string.Concat(
                SevenBitIntroducer,
                imageData.AsSpan(1, imageData.Length - 2),
                SevenBitTerminator);
        }
        else
        {
            framed = $"{SevenBitIntroducer}q{imageData}{SevenBitTerminator}";
        }

        var parseResult = SixelParser.ParsePayload(framed);
        if (parseResult.Outcome != SixelParseOutcome.Complete)
        {
            var diagnostic = parseResult.Diagnostics.FirstOrDefault();
            var detail = string.IsNullOrEmpty(diagnostic.Message)
                ? parseResult.Outcome.ToString()
                : diagnostic.Message;
            throw new ArgumentException($"The value is not a complete valid Sixel sequence: {detail}", parameterName);
        }

        return framed;
    }

    internal static void ValidateCellSpan(
        SixelParseResult parseResult,
        SixelCellMetrics metrics,
        int cellWidth,
        int cellHeight,
        string parameterName)
    {
        var naturalWidth = metrics.ColumnsFor(parseResult.LogicalCanvasExtent.Width);
        var naturalHeight = metrics.RowsFor(parseResult.LogicalCanvasExtent.Height);
        if (naturalWidth != cellWidth || naturalHeight != cellHeight)
        {
            throw new ArgumentException(
                $"Pre-encoded Sixel data occupies {naturalWidth}x{naturalHeight} cells with the active " +
                $"Sixel metrics and cannot be resized to {cellWidth}x{cellHeight}. Use structured pixels " +
                "when resizing is required.",
                parameterName);
        }
    }
}
