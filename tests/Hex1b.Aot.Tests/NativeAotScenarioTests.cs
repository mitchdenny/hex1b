using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Aot.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("NativeAot")]
public sealed class NativeAotScenarioTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task DirectTerminal_NativePublish_ExcludesTapeAndCreationCode()
        => ScenarioRunner.RunAsync(Scenario.DirectTerminal, TestContext, TestContext.CancellationToken);

    [TestMethod]
    public Task ExistingTerminalTape_NativePublish_ExcludesCreationCode()
        => ScenarioRunner.RunAsync(Scenario.ExistingTerminalTape, TestContext, TestContext.CancellationToken);

    [TestMethod]
    public Task CreatedTerminalTape_NativePublish_RetainsCreationAndCleansUp()
        => ScenarioRunner.RunAsync(Scenario.CreatedTerminalTape, TestContext, TestContext.CancellationToken);
}
