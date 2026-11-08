using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
public sealed class ScenarioInfrastructureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("0", false)]
    [DataRow("true", false)]
    [DataRow("1", true)]
    public void IsEnabled_ExplicitSwitch_RequiresOne(string? value, bool expected)
        => Assert.AreEqual(expected, NativeAotEnabledAttribute.IsEnabled(value));

    [TestMethod]
    public async Task CreateProject_EmbeddedSource_PreservesScopedSettingsAndProjectReference()
    {
        var directory = ScenarioRunner.CreateArtifacts("Generated Project " + Guid.NewGuid().ToString("N"));
        const string source = """Console.WriteLine("probe");""";
        var projectPath = await ScenarioRunner.CreateProjectAsync(directory, "Probe", source, TestContext.CancellationToken);
        var project = XDocument.Load(projectPath).Root!;
        var propsPath = Path.Combine(directory, "source", "Directory.Build.props");
        var props = XDocument.Load(propsPath).Root!;
        Assert.AreEqual("Microsoft.NET.Sdk", (string?)project.Attribute("Sdk"));
        Assert.IsEmpty(project.Descendants("RuntimeHostConfigurationOption"));
        Assert.AreEqual(Path.Combine(ScenarioRunner.RepositoryRoot, "src", "Hex1b", "Hex1b.csproj"),
            (string?)project.Element("ItemGroup")!.Element("ProjectReference")!.Attribute("Include"));
        Assert.AreEqual(Path.Combine(ScenarioRunner.RepositoryRoot, "Directory.Build.props"),
            (string?)props.Element("Import")!.Attribute("Project"));
        var properties = props.Element("PropertyGroup")!;
        Assert.AreEqual("net10.0", (string?)properties.Element("TargetFramework"));
        Assert.AreEqual("true", (string?)properties.Element("PublishAot"));
        Assert.AreEqual("Size", (string?)properties.Element("OptimizationPreference"));
        Assert.AreEqual("true", (string?)properties.Element("IlcGenerateMapFile"));
        Assert.AreEqual(Path.Combine(directory, "obj") + Path.DirectorySeparatorChar,
            (string?)properties.Element("BaseIntermediateOutputPath"));
        Assert.AreEqual(Path.Combine(directory, "publish") + Path.DirectorySeparatorChar,
            (string?)properties.Element("PublishDir"));
        Assert.AreEqual(source, await File.ReadAllTextAsync(Path.Combine(directory, "source", "Program.cs"), TestContext.CancellationToken));
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(directory, "source", "EchoWorkload.cs"),
            TestContext.CancellationToken), "IHex1bTerminalWorkloadAdapter");
        TestContext.AddResultFile(projectPath);
        TestContext.AddResultFile(propsPath);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateProject_FeatureSwitch_RemainsConsumerScopedAndTrimmable(bool supported)
    {
        var scenario = Scenario.HeadlessSurfacePooling(supported);
        var directory = ScenarioRunner.CreateArtifacts(scenario.Name + Guid.NewGuid().ToString("N"));
        var projectPath = await ScenarioRunner.CreateProjectAsync(directory, scenario.ConsumerName,
            """Console.WriteLine("probe");""", TestContext.CancellationToken, scenario.FeatureSwitches);
        var project = XDocument.Load(projectPath);
        var setting = TestSeq.Single(project.Descendants("RuntimeHostConfigurationOption"));
        Assert.AreEqual(Scenario.PoolingSwitch, (string?)setting.Attribute("Include"));
        Assert.AreEqual(supported ? "true" : "false", (string?)setting.Attribute("Value"));
        Assert.AreEqual("true", (string?)setting.Attribute("Trim"));
        Assert.IsEmpty(XDocument.Load(Path.Combine(directory, "source", "Directory.Build.props"))
            .Descendants("RuntimeHostConfigurationOption"));
        Assert.IsEmpty(project.Descendants("IlcSubstitution"));
        Assert.AreEqual(supported, scenario.FeatureSwitches[Scenario.PoolingSwitch]);
        TestContext.AddResultFile(projectPath);
    }
}
