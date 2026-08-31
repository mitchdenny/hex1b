using System.Collections.ObjectModel;
using KgpValidation.Scenarios;

namespace KgpValidation;

/// <summary>
/// Ordered scenario catalog used by both the interactive workload and tests.
/// </summary>
internal static class KgpScenarioCatalog
{
    public static IReadOnlyList<KgpValidationScenario> All { get; } =
        new ReadOnlyCollection<KgpValidationScenario>(
        [
            new OverviewScenario(),
            new DirectAndChunkedScenario(),
            new SharedAndReplacementScenario(),
            new SourceCropScenario(),
            new ZOrderScenario(),
            new ScrollingScenario(),
            new UnicodePlaceholderScenario(),
            new RelativePlacementScenario(),
            new AnimationFrameScenario(),
            new DeletionReuseScenario(),
        ]);
}
