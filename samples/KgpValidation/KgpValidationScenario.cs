namespace KgpValidation;

/// <summary>
/// Describes one independently renderable KGP compliance page.
/// </summary>
/// <remarks>
/// A scenario owns the smallest protocol recipe that demonstrates its feature.
/// Keep scenarios deterministic and visually simple: future debugging should be
/// able to compare the rendered page with <see cref="Expected"/> without reading
/// unrelated sample code.
/// </remarks>
internal abstract class KgpValidationScenario
{
    /// <summary>Stable identifier used in diagnostics and tests.</summary>
    public abstract string Id { get; }

    /// <summary>Short page title.</summary>
    public abstract string Title { get; }

    /// <summary>The KGP compliance area exercised by this page.</summary>
    public abstract string Area { get; }

    /// <summary>Human-verifiable description of the correct final screen.</summary>
    public abstract string Expected { get; }

    /// <summary>Short summary of the protocol operations used by the page.</summary>
    public abstract string Protocol { get; }

    /// <summary>Expected Hex1b KGP state after the page has rendered.</summary>
    public abstract KgpScenarioExpectation ExpectedState { get; }

    /// <summary>Number of user-selectable variants on this page.</summary>
    public virtual int VariantCount => 1;

    /// <summary>Optional instruction shown above the navigation footer.</summary>
    public virtual string? ActionHint => null;

    /// <summary>Gets the expected result for a specific page variant.</summary>
    public virtual string GetExpected(int variant)
    {
        if (variant != 0)
            throw new ArgumentOutOfRangeException(nameof(variant));
        return Expected;
    }

    /// <summary>Writes this scenario's text guides and KGP commands.</summary>
    public abstract void Render(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout);

    /// <summary>Writes a selected variant of this scenario.</summary>
    public virtual void RenderVariant(
        KgpProtocolWriter writer,
        KgpScenarioLayout layout,
        int variant)
    {
        if (variant != 0)
            throw new ArgumentOutOfRangeException(nameof(variant));
        Render(writer, layout);
    }
}

/// <summary>
/// Machine-checkable state paired with a human-verifiable scenario.
/// </summary>
internal sealed record KgpScenarioExpectation(
    int ImageCount,
    int PlacementCount,
    int VirtualPlacementCount = 0,
    uint? FrameImageId = null,
    int? FrameCount = null);

/// <summary>
/// Shared terminal geometry for every validation page.
/// </summary>
internal readonly record struct KgpScenarioLayout(int Width, int Height)
{
    public const int MinimumWidth = 80;
    public const int MinimumHeight = 24;
    public const int GraphicsTop = 9;

    public bool HasGraphicsRoom =>
        Width >= MinimumWidth && Height >= MinimumHeight;

    public int GraphicsBottom => Height - 2;
}
