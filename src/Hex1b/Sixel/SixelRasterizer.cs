using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Describes whether a rasterization produced pixels or only geometry.
/// </summary>
/// <remarks>
/// A <see cref="GeometryOnly"/> outcome means the authoritative rasterizer
/// explicitly refused pixel allocation (a bounded resource limit, or a parse
/// outcome that carried no rasterable data), not a bug: the placement is
/// still retained with its declared geometry and explanatory
/// <see cref="Hex1b.SixelData.RasterDiagnostics"/>, never silently dropped.
/// </remarks>
public enum SixelRasterStatus
{
    /// <summary>Pixels are available through <see cref="Hex1b.SixelData.GetPixels"/>.</summary>
    Rasterized,

    /// <summary>
    /// Allocation was explicitly refused or the payload carried no rasterable
    /// data. Geometry and diagnostics remain available.
    /// </summary>
    GeometryOnly,
}

/// <summary>
/// Explicit reasons a rasterization degraded or annotated its result,
/// surfaced via <see cref="SixelRasterDiagnostic"/> on
/// <see cref="Hex1b.SixelData.RasterDiagnostics"/>.
/// </summary>
public enum SixelRasterDiagnosticCode
{
    /// <summary>The parse outcome (cancelled/malformed/rejected) carried no complete graphic to rasterize.</summary>
    ParseOutcomeNotRasterable,

    /// <summary>Bounded command retention truncated the sequence, leaving only geometry and palette state.</summary>
    CommandsIncomplete,

    /// <summary>The sequence produced no logical raster extent.</summary>
    NoRasterableExtent,

    /// <summary>The logical raster extent exceeded the implementation coordinate limit.</summary>
    RasterExtentOverflow,

    /// <summary>The logical pixel count exceeded the configured raster pixel limit.</summary>
    RasterPixelLimitExceeded,

    /// <summary>The number of requested pixel writes exceeded the configured raster operation limit.</summary>
    RasterOperationLimitExceeded,

    /// <summary>A tiled-raster resource limit was exceeded.</summary>
    RasterTileLimitExceeded,

    /// <summary>A referenced color register fell outside the compatibility policy's accepted range.</summary>
    ColorRegisterOutOfPolicy,
}

/// <summary>
/// A single explicit rasterization diagnostic explaining a geometry-only
/// downgrade or other annotated raster outcome.
/// </summary>
/// <param name="Code">The specific reason this diagnostic was raised.</param>
/// <param name="Message">A human-readable explanation.</param>
public readonly record struct SixelRasterDiagnostic(
    SixelRasterDiagnosticCode Code,
    string Message);

/// <summary>
/// The separate extents preserved for downstream placement and translation.
/// </summary>
/// <param name="Logical">The unscaled canvas; six logical rows per Sixel band.</param>
/// <param name="Rendered">The logical canvas after applying the pixel aspect ratio.</param>
/// <param name="Declared">The DECGRA <c>Ph</c>/<c>Pv</c> hint, or empty when absent.</param>
/// <param name="Data">The unscaled extent reached by data commands.</param>
/// <param name="Painted">The unscaled bounds of explicitly painted pixels.</param>
/// <param name="Aspect">The effective pixel aspect ratio.</param>
public readonly record struct SixelRasterExtents(
    SixelExtent Logical,
    SixelExtent Rendered,
    SixelExtent Declared,
    SixelExtent Data,
    SixelBounds Painted,
    SixelAspectRatio Aspect)
{
    /// <summary>An empty set of extents, using the default 2:1 aspect ratio.</summary>
    public static SixelRasterExtents Empty { get; } = new(
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelBounds.Empty,
        new SixelAspectRatio(2, 1));
}

/// <summary>
/// The environment a Sixel graphic is rasterized against.
/// </summary>
/// <param name="Background">The background captured when the graphic was created.</param>
/// <param name="Registers">The terminal-scoped color registers mutated in command order.</param>
/// <param name="Policy">The compatibility and resource policy.</param>
internal sealed record SixelRasterEnvironment(
    Rgba32 Background,
    SixelColorRegisters Registers,
    SixelCompatibilityPolicy Policy)
{
    /// <summary>
    /// Creates an environment with the deterministic default background and a
    /// fresh default palette. Used when no terminal state is available.
    /// </summary>
    public static SixelRasterEnvironment CreateDefault()
    {
        var policy = SixelCompatibilityPolicy.Default;
        return new SixelRasterEnvironment(
            policy.DefaultBackground,
            new SixelColorRegisters(policy),
            policy);
    }
}

/// <summary>
/// Captures the immutable inputs needed to rasterize a Sixel graphic lazily.
/// </summary>
/// <param name="Environment">
/// A private register snapshot and the background captured when the graphic was created.
/// </param>
/// <param name="Identity">
/// A deterministic identity for the captured background, palette, and policy.
/// </param>
internal sealed record SixelRasterPreparation(
    SixelRasterEnvironment Environment,
    byte[] Identity);

/// <summary>
/// The authoritative bounded rasterization of one Sixel sequence.
/// </summary>
internal sealed record SixelRasterResult(
    SixelRasterStatus Status,
    SixelRasterExtents Extents,
    SixelRasterImage? Image,
    SixelBackgroundMode BackgroundMode,
    Rgba32 UnpaintedPixel,
    byte[] Identity,
    IReadOnlyList<SixelRasterDiagnostic> Diagnostics);

/// <summary>
/// Rasterizes the authoritative structured Sixel parse result.
/// </summary>
/// <remarks>
/// <para>
/// The rasterizer never rescans raw DCS bytes. It consumes
/// <see cref="SixelParseResult.Commands"/>, which already carry absolute column
/// positions and logical band indices, so logical row positions survive even
/// though the parser's own geometry is aspect-scaled.
/// </para>
/// <para>
/// Color register mutations are applied to the supplied terminal-scoped register
/// file in command order, which is what makes the palette persist between
/// sequences.
/// </para>
/// </remarks>
internal static class SixelRasterizer
{
    private const int BandRows = 6;

    /// <summary>
    /// Captures the state required for lazy rasterization and applies palette
    /// definitions immediately to the terminal-scoped register file.
    /// </summary>
    public static SixelRasterPreparation Prepare(
        SixelParseResult parse,
        SixelRasterEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(environment);

        var capturedEnvironment = environment with
        {
            Registers = environment.Registers.Snapshot(),
        };
        if (environment.Policy.PaletteScope == SixelPaletteScope.TerminalPersistent)
        {
            ApplyPersistentPaletteMutations(parse, environment.Registers);
        }
        return new SixelRasterPreparation(
            capturedEnvironment,
            BuildPreparationIdentity(parse, capturedEnvironment));
    }

    public static SixelRasterResult Rasterize(
        SixelParseResult parse,
        SixelRasterEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(environment);

        var policy = environment.Policy;
        var diagnostics = new List<SixelRasterDiagnostic>();
        var aspect = parse.Header.AspectRatio;
        var declared = parse.DeclaredExtent;

        if (parse.Outcome is SixelParseOutcome.Cancelled or
            SixelParseOutcome.Malformed or
            SixelParseOutcome.Rejected)
        {
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.ParseOutcomeNotRasterable,
                $"The parse outcome '{parse.Outcome}' does not describe a complete graphic."));
            return GeometryOnly(
                SixelRasterExtents.Empty with { Declared = declared, Aspect = aspect },
                parse.Header.BackgroundMode,
                environment,
                diagnostics);
        }

        var measurement = Measure(parse, declared, aspect);

        if (!parse.CommandsComplete)
        {
            ApplyPaletteMutations(parse.PaletteMutations, environment, diagnostics);
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.CommandsIncomplete,
                "Raster command retention was bounded, so only geometry and palette state are available."));
            return GeometryOnly(measurement.Extents, parse.Header.BackgroundMode, environment, diagnostics);
        }

        if (measurement.Overflowed)
        {
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.RasterExtentOverflow,
                "The logical raster extent exceeded the implementation coordinate limit."));
        }

        var unpainted = ResolveUnpaintedPixel(parse.Header.BackgroundMode, environment);
        var logical = measurement.Extents.Logical;
        if (logical.Width <= 0 || logical.Height <= 0)
        {
            ApplyPaletteOnly(parse, environment, diagnostics);
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.NoRasterableExtent,
                "The sequence produced no logical raster extent."));
            return GeometryOnly(measurement.Extents, parse.Header.BackgroundMode, environment, diagnostics);
        }

        var pixelCount = (long)logical.Width * logical.Height;
        if (measurement.Overflowed || pixelCount > policy.MaximumRasterPixels)
        {
            ApplyPaletteOnly(parse, environment, diagnostics);
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.RasterPixelLimitExceeded,
                $"A {logical.Width}x{logical.Height} raster exceeds the {policy.MaximumRasterPixels} pixel limit."));
            return GeometryOnly(measurement.Extents, parse.Header.BackgroundMode, environment, diagnostics);
        }

        if (measurement.RasterOperations > policy.MaximumRasterOperations)
        {
            ApplyPaletteOnly(parse, environment, diagnostics);
            diagnostics.Add(new SixelRasterDiagnostic(
                SixelRasterDiagnosticCode.RasterOperationLimitExceeded,
                $"The sequence requests {measurement.RasterOperations} pixel writes, above the " +
                $"{policy.MaximumRasterOperations} limit."));
            return GeometryOnly(measurement.Extents, parse.Header.BackgroundMode, environment, diagnostics);
        }

        var image = new SixelRasterImage(logical.Width, logical.Height, unpainted, policy);
        var identity = new RasterIdentityBuilder(environment.Background, parse.Header.BackgroundMode);
        var selected = 0;
        var tileLimitExceeded = false;

        foreach (var command in parse.Commands)
        {
            if (command.Kind == SixelCommandKind.Palette)
            {
                if (command.Palette is { } palette)
                {
                    selected = ApplyPaletteCommand(palette, environment, diagnostics, selected);
                }

                continue;
            }

            if (command.Value == 0 || command.RepeatCount <= 0)
            {
                continue;
            }

            if (tileLimitExceeded)
            {
                continue;
            }

            if (!environment.Registers.IsWithinPolicy(selected))
            {
                continue;
            }

            var color = environment.Registers.Get(selected);
            identity.Observe(selected, color);

            var startX = command.X;
            var endX = SaturatingAdd(startX, command.RepeatCount);
            endX = Math.Min(endX, logical.Width);
            var bandTop = SaturatingMultiply(command.Band, BandRows);

            for (var bit = 0; bit < BandRows; bit++)
            {
                if ((command.Value & (1 << bit)) == 0)
                {
                    continue;
                }

                var y = SaturatingAdd(bandTop, bit);
                if (y >= logical.Height)
                {
                    continue;
                }

                for (var x = startX; x < endX; x++)
                {
                    if (image.TryPaint(x, y, color))
                    {
                        continue;
                    }

                    tileLimitExceeded = true;
                    diagnostics.Add(new SixelRasterDiagnostic(
                        SixelRasterDiagnosticCode.RasterTileLimitExceeded,
                        "Sparse raster tile allocation was refused at its bounded limit."));
                    break;
                }

                if (tileLimitExceeded)
                {
                    break;
                }
            }
        }

        if (tileLimitExceeded)
        {
            return GeometryOnly(
                measurement.Extents,
                parse.Header.BackgroundMode,
                environment,
                diagnostics);
        }

        return new SixelRasterResult(
            SixelRasterStatus.Rasterized,
            measurement.Extents,
            image,
            parse.Header.BackgroundMode,
            unpainted,
            identity.Build(SixelRasterStatus.Rasterized, measurement.Extents),
            diagnostics);
    }

    private static SixelRasterResult GeometryOnly(
        SixelRasterExtents extents,
        SixelBackgroundMode backgroundMode,
        SixelRasterEnvironment environment,
        List<SixelRasterDiagnostic> diagnostics)
    {
        var unpainted = ResolveUnpaintedPixel(backgroundMode, environment);
        var identity = new RasterIdentityBuilder(environment.Background, backgroundMode);
        return new SixelRasterResult(
            SixelRasterStatus.GeometryOnly,
            extents,
            null,
            backgroundMode,
            unpainted,
            identity.Build(SixelRasterStatus.GeometryOnly, extents),
            diagnostics);
    }

    private static Rgba32 ResolveUnpaintedPixel(
        SixelBackgroundMode mode,
        SixelRasterEnvironment environment)
    {
        if (mode == SixelBackgroundMode.Transparent)
        {
            return Rgba32.Transparent;
        }

        return environment.Policy.BackgroundSource switch
        {
            SixelBackgroundSource.PaletteRegisterZero when environment.Registers.IsWithinPolicy(0) =>
                environment.Registers.Get(0),
            _ => environment.Background,
        };
    }

    private static void ApplyPaletteOnly(
        SixelParseResult parse,
        SixelRasterEnvironment environment,
        List<SixelRasterDiagnostic> diagnostics)
    {
        var selected = 0;
        foreach (var command in parse.Commands)
        {
            if (command.Kind == SixelCommandKind.Palette && command.Palette is { } palette)
            {
                selected = ApplyPaletteCommand(palette, environment, diagnostics, selected);
            }
        }
    }

    private static void ApplyPaletteMutations(
        IReadOnlyList<SixelPaletteCommand> mutations,
        SixelRasterEnvironment environment,
        List<SixelRasterDiagnostic> diagnostics)
    {
        var selected = 0;
        foreach (var mutation in mutations)
        {
            selected = ApplyPaletteCommand(mutation, environment, diagnostics, selected);
        }
    }

    private static int ApplyPaletteCommand(
        SixelPaletteCommand palette,
        SixelRasterEnvironment environment,
        List<SixelRasterDiagnostic> diagnostics,
        int selected)
    {
        if (!environment.Registers.IsWithinPolicy(palette.Register))
        {
            if (!diagnostics.Any(item => item.Code == SixelRasterDiagnosticCode.ColorRegisterOutOfPolicy))
            {
                diagnostics.Add(new SixelRasterDiagnostic(
                    SixelRasterDiagnosticCode.ColorRegisterOutOfPolicy,
                    $"Color register {palette.Register} is outside the configured " +
                    $"{environment.Registers.Count}-register policy and was rejected."));
            }

            return selected;
        }

        if (palette.IsDefinition)
        {
            environment.Registers.Define(palette.Register, SixelColorConverter.FromDefinition(palette));
        }

        return palette.Register;
    }

    private static void ApplyPersistentPaletteMutations(
        SixelParseResult parse,
        SixelColorRegisters registers)
    {
        if (!parse.CommandsComplete)
        {
            foreach (var mutation in parse.PaletteMutations)
            {
                ApplyPersistentPaletteDefinition(mutation, registers);
            }

            return;
        }

        foreach (var command in parse.Commands)
        {
            if (command.Palette is { } mutation)
            {
                ApplyPersistentPaletteDefinition(mutation, registers);
            }
        }
    }

    private static void ApplyPersistentPaletteDefinition(
        SixelPaletteCommand mutation,
        SixelColorRegisters registers)
    {
        if (mutation.IsDefinition && registers.IsWithinPolicy(mutation.Register))
        {
            registers.Define(
                mutation.Register,
                SixelColorConverter.FromDefinition(mutation));
        }
    }

    private static byte[] BuildPreparationIdentity(
        SixelParseResult parse,
        SixelRasterEnvironment environment)
    {
        var registers = environment.Registers.Snapshot();
        var used = new HashSet<(int Register, uint Color)>();
        var selected = 0;

        foreach (var command in parse.Commands)
        {
            if (command.Kind == SixelCommandKind.Palette)
            {
                if (command.Palette is { } palette)
                {
                    if (registers.IsWithinPolicy(palette.Register))
                    {
                        selected = palette.Register;
                        if (palette.IsDefinition)
                        {
                            registers.Define(
                                palette.Register,
                                SixelColorConverter.FromDefinition(palette));
                        }
                    }
                }

                continue;
            }

            if (command.Value == 0 ||
                command.RepeatCount <= 0 ||
                !registers.IsWithinPolicy(selected))
            {
                continue;
            }

            var color = registers.Get(selected);
            used.Add((
                selected,
                ((uint)color.A << 24) |
                ((uint)color.R << 16) |
                ((uint)color.G << 8) |
                color.B));
        }

        var buffer = new List<byte>(32 + (used.Count * 8))
        {
            environment.Background.R,
            environment.Background.G,
            environment.Background.B,
            environment.Background.A,
            (byte)parse.Header.BackgroundMode,
        };
        buffer.AddRange(BitConverter.GetBytes(environment.Policy.ColorRegisterCount));
        buffer.AddRange(BitConverter.GetBytes(environment.Policy.MaximumRasterPixels));
        buffer.AddRange(BitConverter.GetBytes(environment.Policy.MaximumRasterOperations));
        buffer.AddRange(BitConverter.GetBytes(environment.Policy.MaximumRasterTiles));
        buffer.AddRange(BitConverter.GetBytes(environment.Policy.RasterTileSize));
        foreach (var (register, color) in used.OrderBy(item => item.Register).ThenBy(item => item.Color))
        {
            buffer.AddRange(BitConverter.GetBytes(register));
            buffer.AddRange(BitConverter.GetBytes(color));
        }

        return SHA256.HashData([.. buffer])[..16];
    }

    private static Measurement Measure(
        SixelParseResult parse,
        SixelExtent declared,
        SixelAspectRatio aspect)
    {
        var dataWidth = 0;
        var dataHeight = 0;
        var paintedMinX = int.MaxValue;
        var paintedMinY = int.MaxValue;
        var paintedMaxX = 0;
        var paintedMaxY = 0;
        long operations = 0;
        var overflowed = false;

        foreach (var command in parse.Commands)
        {
            if (command.Kind != SixelCommandKind.Data || command.RepeatCount <= 0)
            {
                continue;
            }

            var endX = SaturatingAdd(command.X, command.RepeatCount);
            overflowed |= endX == int.MaxValue;
            dataWidth = Math.Max(dataWidth, endX);

            var bandTop = SaturatingMultiply(command.Band, BandRows);
            var bandBottom = SaturatingAdd(bandTop, BandRows);
            overflowed |= bandBottom == int.MaxValue;
            dataHeight = Math.Max(dataHeight, bandBottom);

            if (command.Value == 0)
            {
                continue;
            }

            var firstBit = System.Numerics.BitOperations.TrailingZeroCount((uint)command.Value);
            var lastBit = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)command.Value);
            paintedMinX = Math.Min(paintedMinX, command.X);
            paintedMaxX = Math.Max(paintedMaxX, endX);
            paintedMinY = Math.Min(paintedMinY, SaturatingAdd(bandTop, firstBit));
            paintedMaxY = Math.Max(paintedMaxY, SaturatingAdd(bandTop, lastBit + 1));

            operations += (long)command.RepeatCount *
                System.Numerics.BitOperations.PopCount((uint)command.Value);
        }

        var painted = paintedMinX == int.MaxValue
            ? SixelBounds.Empty
            : new SixelBounds(
                paintedMinX,
                paintedMinY,
                paintedMaxX - paintedMinX,
                paintedMaxY - paintedMinY);

        var logical = new SixelExtent(
            Math.Max(declared.Width, Math.Max(dataWidth, paintedMaxX)),
            Math.Max(declared.Height, Math.Max(dataHeight, paintedMaxY)));
        var renderedHeight = ScaleHeight(logical.Height, aspect, ref overflowed);

        return new Measurement(
            new SixelRasterExtents(
                logical,
                new SixelExtent(logical.Width, renderedHeight),
                declared,
                new SixelExtent(dataWidth, dataHeight),
                painted,
                aspect),
            operations,
            overflowed);
    }

    private static int ScaleHeight(int height, SixelAspectRatio aspect, ref bool overflowed)
    {
        if (aspect.Numerator <= 0 || aspect.Denominator <= 0)
        {
            return height;
        }

        var scaled = (((long)height * aspect.Numerator) + aspect.Denominator - 1) / aspect.Denominator;
        if (scaled > int.MaxValue)
        {
            overflowed = true;
            return int.MaxValue;
        }

        return (int)scaled;
    }

    private static int SaturatingAdd(int left, int right) =>
        right <= int.MaxValue - left ? left + right : int.MaxValue;

    private static int SaturatingMultiply(int left, int right) =>
        right == 0 || left <= int.MaxValue / right ? left * right : int.MaxValue;

    private readonly record struct Measurement(
        SixelRasterExtents Extents,
        long RasterOperations,
        bool Overflowed);

    /// <summary>
    /// Builds a compact identity for the raster state that produced a graphic.
    /// </summary>
    /// <remarks>
    /// Identical payloads raster differently when the captured background or the
    /// persistent palette differ, so this identity participates in tracked-object
    /// deduplication instead of the payload hash alone.
    /// </remarks>
    private sealed class RasterIdentityBuilder(Rgba32 background, SixelBackgroundMode mode)
    {
        private readonly HashSet<(int Register, uint Color)> _used = [];

        public void Observe(int register, Rgba32 color) =>
            _used.Add((register, ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B));

        public byte[] Build(SixelRasterStatus status, SixelRasterExtents extents)
        {
            var buffer = new List<byte>(32 + (_used.Count * 8))
            {
                background.R,
                background.G,
                background.B,
                background.A,
                (byte)mode,
                (byte)status,
            };
            buffer.AddRange(BitConverter.GetBytes(extents.Logical.Width));
            buffer.AddRange(BitConverter.GetBytes(extents.Logical.Height));
            foreach (var (register, color) in _used.OrderBy(item => item.Register).ThenBy(item => item.Color))
            {
                buffer.AddRange(BitConverter.GetBytes(register));
                buffer.AddRange(BitConverter.GetBytes(color));
            }

            return SHA256.HashData([.. buffer])[..16];
        }
    }
}
