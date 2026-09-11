namespace Hex1b.Tests;

[TestClass]
public class Hex1bTerminalGraphicsOptionsTests
{
    [TestMethod]
    public void Defaults_PreserveExistingGraphicsLimits()
    {
        var options = new Hex1bTerminalGraphicsOptions();

        Assert.AreEqual(1024 * 1024, options.MaximumRetainedInputBytesPerImage);
        Assert.AreEqual(16L * 1024 * 1024, options.MaximumRasterPixelsPerImage);
        Assert.AreEqual(64L * 1024 * 1024, options.MaximumRasterOperationsPerImage);
        Assert.AreEqual(1_024, options.MaximumImagesPerScreen);
        Assert.AreEqual(4_096, options.MaximumPlacementsPerScreen);
        Assert.AreEqual(4_096, options.MaximumHistoryPlacements);
        Assert.AreEqual(
            64L * 1024 * 1024,
            options.MaximumRetainedLogicalPixelsPerScreen);
        Assert.AreEqual(320L * 1024 * 1024, options.MaximumRetainedBytesPerScreen);

        var policy = new Hex1bTerminalOptions().CreateSixelPolicy();
        Assert.AreEqual(4_096, policy.MaximumRasterTiles);
    }

    [TestMethod]
    public void MaximumRasterPixels_MaximumValue_DerivesTileCountWithoutOverflow()
    {
        var options = new Hex1bTerminalOptions
        {
            Graphics = new Hex1bTerminalGraphicsOptions
            {
                MaximumRasterPixelsPerImage = int.MaxValue,
            },
        };

        var policy = options.CreateSixelPolicy();

        Assert.AreEqual(524_288, policy.MaximumRasterTiles);
    }

    [TestMethod]
    public void PublicApi_IsAccessibleFromTerminalOptionsAndBuilder()
    {
        Assert.IsTrue(typeof(Hex1bTerminalGraphicsOptions).IsPublic);
        Assert.IsTrue(
            typeof(Hex1bTerminalOptions)
                .GetProperty(nameof(Hex1bTerminalOptions.Graphics))!
                .GetMethod!
                .IsPublic);
        Assert.IsTrue(
            typeof(Hex1bTerminalBuilder)
                .GetMethod(
                    nameof(Hex1bTerminalBuilder.WithGraphics),
                    [typeof(Action<Hex1bTerminalGraphicsOptions>)])!
                .IsPublic);
    }

    [TestMethod]
    public void Construction_InvalidGraphicsValues_ThrowsPreciseExceptions()
    {
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRetainedInputBytesPerImage),
            options => options.MaximumRetainedInputBytesPerImage = -1);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRasterPixelsPerImage),
            options => options.MaximumRasterPixelsPerImage = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRasterPixelsPerImage),
            options => options.MaximumRasterPixelsPerImage = (long)int.MaxValue + 1);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRasterOperationsPerImage),
            options => options.MaximumRasterOperationsPerImage = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumImagesPerScreen),
            options => options.MaximumImagesPerScreen = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumPlacementsPerScreen),
            options => options.MaximumPlacementsPerScreen = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumHistoryPlacements),
            options => options.MaximumHistoryPlacements = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRetainedLogicalPixelsPerScreen),
            options => options.MaximumRetainedLogicalPixelsPerScreen = 0);
        AssertInvalid(
            nameof(Hex1bTerminalGraphicsOptions.MaximumRetainedBytesPerScreen),
            options => options.MaximumRetainedBytesPerScreen = -1);
    }

    [TestMethod]
    public void Construction_NullGraphicsOptions_ThrowsPreciseException()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            Graphics = null!,
        };

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new Hex1bTerminal(options));

        Assert.AreEqual(nameof(Hex1bTerminalOptions.Graphics), exception.ParamName);
    }

    [TestMethod]
    public void Construction_ZeroByteBudgets_AreAllowed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            Graphics = new Hex1bTerminalGraphicsOptions
            {
                MaximumRetainedInputBytesPerImage = 0,
                MaximumRetainedBytesPerScreen = 0,
            },
        });
    }

    [TestMethod]
    public void Construction_ConflictingInternalAndPublicLimits_Throws()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            SixelPolicy = global::Hex1b.Sixel.SixelCompatibilityPolicy.Default with
            {
                MaximumImagesPerScreen = 1,
            },
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => new Hex1bTerminal(options));

        StringAssert.Contains(
            exception.Message,
            nameof(global::Hex1b.Sixel.SixelCompatibilityPolicy.MaximumImagesPerScreen));
        StringAssert.Contains(
            exception.Message,
            nameof(Hex1bTerminalGraphicsOptions.MaximumImagesPerScreen));
    }

    [TestMethod]
    public void Construction_MatchingInternalAndPublicLimits_IsAllowed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            SixelPolicy = global::Hex1b.Sixel.SixelCompatibilityPolicy.Default with
            {
                MaximumImagesPerScreen = 1,
            },
            Graphics = new Hex1bTerminalGraphicsOptions
            {
                MaximumImagesPerScreen = 1,
            },
        });
    }

    [TestMethod]
    public void Builder_NullGraphicsConfiguration_Throws()
    {
        var builder = Hex1bTerminal.CreateBuilder();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.WithGraphics(null!));
    }

    [TestMethod]
    public void Builder_InvalidGraphicsConfiguration_ThrowsDuringBuild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithGraphics(options => options.MaximumImagesPerScreen = 0);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => builder.Build());

        Assert.AreEqual(
            nameof(Hex1bTerminalGraphicsOptions.MaximumImagesPerScreen),
            exception.ParamName);
    }

    private static void AssertInvalid(
        string expectedParameterName,
        Action<Hex1bTerminalGraphicsOptions> configure)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var graphics = new Hex1bTerminalGraphicsOptions();
        configure(graphics);
        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            Graphics = graphics,
        };

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new Hex1bTerminal(options));

        Assert.AreEqual(expectedParameterName, exception.ParamName);
    }
}
