using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Aot.Tests;

[TestClass]
[TestCategory("MapParser")]
public sealed class NativeMapTests
{
    private static IReadOnlyList<MethodCode> Read(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return NativeMap.Read(stream);
    }

    [TestMethod]
    public void Read_MethodCodeAndEscapedGenerics_IgnoresMetadata()
    {
        var methods = Read("""
            <ObjectNodes>
              <EEType Name="Hex1b_Hex1b_Hex1bTerminalBuilder" Length="80" />
              <MethodCode Name="Generic&lt;Hex1b_Hex1b_Hex1bTerminalBuilder&gt;" Length="12" />
              <MethodCode Name="System__Generic&lt;Hex1b_Hex1b_Hex1bTerminalBuilder__Build&gt;__Invoke" Length="13" />
              <MethodCode Name="System__Start&lt;Hex1b_Hex1b_Automation_TapePlayer__PlayWithCreatedTerminalAsync_d__5&gt;" Length="14" />
              <MethodCode Name="Hex1b_Hex1b_Hex1bTerminal__Start" Length="42" />
              <MethodCode Name="Hex1b_Hex1b_Automation_TapePlayer__PlayWithExistingTerminalAsync" Length="17" />
            </ObjectNodes>
            """);
        Assert.AreEqual("Generic<Hex1b_Hex1b_Hex1bTerminalBuilder>", methods[0].Name);
        var evidence = NativeMap.Measure(methods, Scenario.ExistingTerminalTape.Required, Scenario.ExistingTerminalTape.Forbidden);
        NativeMap.Validate(evidence);
        Assert.AreEqual(42L, evidence.Single(e => e.Family == "terminal").Length);
        Assert.AreEqual(0, evidence.Single(e => e.Family == "terminal builder").Count);
        Assert.AreEqual(1, evidence.Single(e => e.Family == "existing-terminal playback").Count);
    }

    [TestMethod]
    public void Validate_ForbiddenGeneratedMethodCode_Fails()
    {
        var methods = Read("""
            <ObjectNodes>
              <MethodCode Name="Hex1b_Hex1b_Hex1bTerminal__Start" Length="42" />
              <MethodCode Name="Hex1b_Hex1b_Automation_TapePlayer__PlayWithExistingTerminalAsync" Length="17" />
              <MethodCode Name="Hex1b_Hex1b_Automation_TapePlayer___PlayWithCreatedTerminalAsync_d__6__MoveNext" Length="120" />
            </ObjectNodes>
            """);
        var evidence = NativeMap.Measure(methods, Scenario.ExistingTerminalTape.Required, Scenario.ExistingTerminalTape.Forbidden);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => NativeMap.Validate(evidence));
        StringAssert.Contains(error.Message, "created-terminal playback: forbidden, count=1, length=120");
    }

    [TestMethod]
    public void Validate_MissingPositiveControl_Fails()
    {
        var evidence = NativeMap.Measure(Read("<ObjectNodes><MethodCode Name='Unrelated__Start' Length='10'/></ObjectNodes>"),
            Scenario.DirectTerminal.Required, Scenario.DirectTerminal.Forbidden);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => NativeMap.Validate(evidence));
        StringAssert.Contains(error.Message, "terminal: required, count=0");
    }

    [TestMethod]
    [DataRow("Hex1b_Hex1b_Hex1bTerminalBuilder___c__DisplayClass1_0___Build_b__0", "terminal builder")]
    [DataRow("Hex1b_Hex1b_Hex1bTerminalChildProcess__StartAsync_d__3__MoveNext", "child-process implementation")]
    [DataRow("Hex1b_Hex1b_WindowsPtyHandle_SECURITY_ATTRIBUTES____GetFieldHelper", "child-process implementation")]
    [DataRow("Hex1b_Hex1b_Automation_TapePlayer___c__DisplayClass5_0___PlayWithCreatedTerminalAsync_b__0", "created-terminal playback")]
    [DataRow("Hex1b_Hex1b_Automation_TapePlayer__ConfigureOwnedBuilder", "ConfigureOwnedBuilder")]
    public void Validate_ForbiddenFamilyIncludingGeneratedCode_Fails(string method, string family)
    {
        var methods = Read($"""
            <ObjectNodes>
              <MethodCode Name="Hex1b_Hex1b_Hex1bTerminal__Start" Length="42" />
              <MethodCode Name="Hex1b_Hex1b_Automation_TapePlayer__PlayWithExistingTerminalAsync" Length="17" />
              <MethodCode Name="{method}" Length="120" />
            </ObjectNodes>
            """);
        var evidence = NativeMap.Measure(methods, Scenario.ExistingTerminalTape.Required, Scenario.ExistingTerminalTape.Forbidden);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => NativeMap.Validate(evidence)).Message,
            $"{family}: forbidden, count=1, length=120");
    }

    [TestMethod]
    public void Read_UnreadableStream_Fails()
    {
        using var stream = new MemoryStream();
        stream.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => NativeMap.Read(stream));
    }

    [TestMethod]
    [DataRow("<Unknown/>")]
    [DataRow("<ObjectNodes/>")]
    [DataRow("<ObjectNodes><EEType Name='MetadataOnly' Length='1'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Length='1'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Name='' Length='1'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Name='Method'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Name='Method' Length='zero'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Name='Method' Length='-1'/></ObjectNodes>")]
    [DataRow("<ObjectNodes><MethodCode Name='Method' Length='0'/></ObjectNodes>")]
    public void Read_UnsupportedOrMissingData_Fails(string xml)
        => Assert.ThrowsExactly<InvalidDataException>(() => Read(xml));

    [TestMethod]
    public void Read_MalformedXml_Fails()
        => Assert.ThrowsExactly<XmlException>(() => Read("<ObjectNodes>"));

    [TestMethod]
    public void Read_MissingFile_Fails()
        => Assert.ThrowsExactly<FileNotFoundException>(() =>
            NativeMap.Read(Path.Combine(ScenarioRunner.RepositoryRoot, "TestResults", Guid.NewGuid() + ".map.xml")));
}
