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
    public async Task CreateProject_ExperimentalSources_ScopesCompileReplacementToLibrary()
    {
        var directory = ScenarioRunner.CreateArtifacts("ExperimentalSources");
        var overrides = HeadlessConsoleTests.CreateOverrides(true, true);
        var projectPath = await ScenarioRunner.CreateProjectAsync(directory, "Probe", """Console.WriteLine("probe");""",
            TestContext.CancellationToken, overrides);
        var reference = XDocument.Load(projectPath).Root!.Element("ItemGroup")!.Element("ProjectReference")!;
        var targetsPath = Path.Combine(directory, "source", "ExperimentalSources.targets");
        Assert.AreEqual("DirectoryBuildTargetsPath=" + targetsPath, (string?)reference.Element("AdditionalProperties"));
        Assert.AreEqual(Path.Combine("overrides", "**", "*.cs"),
            (string?)XDocument.Load(projectPath).Root!.Element("ItemGroup")!.Element("Compile")!.Attribute("Remove"));
        var target = XDocument.Load(targetsPath).Root!.Element("Target")!;
        Assert.AreEqual("'$(MSBuildProjectName)' == 'Hex1b'", (string?)target.Attribute("Condition"));
        Assert.AreEqual("AssignTargetPaths", (string?)target.Attribute("BeforeTargets"));
        var items = target.Element("ItemGroup")!;
        Assert.AreEqual("@(Compile)", (string?)items.Elements("_NativeAotCompile").First().Attribute("Include"));
        Assert.AreEqual("%(Compile.Identity)", items.Elements("_NativeAotCompile").First().Element("ReplacementPath")!.Value);
        Assert.AreEqual("@(Compile)", (string?)items.Elements("Compile").First().Attribute("Remove"));
        Assert.AreEqual("@(_NativeAotCompile->'%(ReplacementPath)')",
            (string?)items.Elements("Compile").Last().Attribute("Include"));
        foreach (var (name, source) in overrides)
        {
            var replacement = items.Elements("_NativeAotCompile").Single(item =>
                (string?)item.Attribute("Condition") == $"'%(_NativeAotCompile.Identity)' == '{name}'");
            Assert.AreEqual(Path.Combine(directory, "source", "overrides", name), replacement.Element("ReplacementPath")!.Value);
            Assert.AreEqual(source, await File.ReadAllTextAsync(
                Path.Combine(directory, "source", "overrides", name), TestContext.CancellationToken));
        }
    }

    [TestMethod]
    public async Task CreateProject_ExperimentalSources_RejectsOutsideLibraryPaths()
    {
        var directory = ScenarioRunner.CreateArtifacts("InvalidExperimentalSources");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            ScenarioRunner.CreateProjectAsync(directory, "Probe", "", TestContext.CancellationToken,
                new Dictionary<string, string> { [Path.Combine("..", "Outside.cs")] = "" }));
    }
}
