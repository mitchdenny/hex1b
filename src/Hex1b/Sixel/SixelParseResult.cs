using Hex1b.Tokens;

namespace Hex1b.Sixel;

internal sealed record SixelParseResult(
    SixelHeader Header,
    SixelRasterAttributes? RasterAttributes,
    SixelPoint GraphicsCursor,
    SixelPoint MaximumCommandOrDataPosition,
    SixelExtent DeclaredExtent,
    SixelExtent DataExtent,
    SixelBounds PaintedBounds,
    SixelExtent LogicalCanvasExtent,
    SixelExtent UnscaledDataExtent,
    SixelBounds UnscaledPaintedBounds,
    SixelExtent UnscaledLogicalCanvasExtent,
    int SelectedColorRegister,
    IReadOnlyList<SixelPaletteCommand> PaletteMutations,
    IReadOnlyList<SixelPaletteCommand> FinalPaletteDefinitions,
    IReadOnlyList<SixelCommand> Commands,
    bool CommandsComplete,
    SixelParseOutcome Outcome,
    IReadOnlyList<SixelDiagnostic> Diagnostics)
{
    public static SixelParseResult Rejected(
        DcsIntroducer introducer,
        DcsSequenceStatus status,
        bool retentionLimitExceeded)
    {
        var outcome = status switch
        {
            DcsSequenceStatus.Cancelled => SixelParseOutcome.Cancelled,
            DcsSequenceStatus.Malformed or DcsSequenceStatus.Unterminated => SixelParseOutcome.Malformed,
            _ when retentionLimitExceeded => SixelParseOutcome.LimitDowngraded,
            _ => SixelParseOutcome.Rejected,
        };
        var code = status == DcsSequenceStatus.Unterminated
            ? SixelDiagnosticCode.UnterminatedSequence
            : SixelDiagnosticCode.RejectedIntroducer;
        var message = status == DcsSequenceStatus.Unterminated
            ? "The DCS sequence ended before a string terminator."
            : "The DCS introducer is not an accepted Sixel form.";

        return new SixelParseResult(
            CreateHeader(introducer.Parameters),
            null,
            new SixelPoint(0, 0),
            new SixelPoint(0, 0),
            SixelExtent.Empty,
            SixelExtent.Empty,
            SixelBounds.Empty,
            SixelExtent.Empty,
            SixelExtent.Empty,
            SixelBounds.Empty,
            SixelExtent.Empty,
            0,
            Array.Empty<SixelPaletteCommand>(),
            Array.Empty<SixelPaletteCommand>(),
            Array.Empty<SixelCommand>(),
            false,
            outcome,
            [new SixelDiagnostic(code, 0, introducer.FinalByte, message)]);
    }

    internal static SixelHeader CreateHeader(
        IReadOnlyList<int?> parameters,
        SixelCompatibilityPolicy? policy = null)
    {
        var p1 = GetParameter(parameters, 0);
        var p2 = GetParameter(parameters, 1);
        var p3 = GetParameter(parameters, 2);
        var aspect = SixelParser.GetAspectRatio(p1);
        return new SixelHeader(
            p1,
            p2,
            p3,
            (policy ?? SixelCompatibilityPolicy.Default).ResolveAspectRatio(aspect),
            p2 == 1 ? SixelBackgroundMode.Transparent : SixelBackgroundMode.Opaque);
    }

    private static int GetParameter(IReadOnlyList<int?> parameters, int index) =>
        index < parameters.Count ? parameters[index] ?? 0 : 0;
}
