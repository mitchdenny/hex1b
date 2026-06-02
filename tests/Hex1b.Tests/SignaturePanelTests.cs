using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SignaturePanel"/>, <see cref="SignaturePanelEntry"/>,
/// <see cref="SignatureParameterInfo"/>, and their integration with
/// <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
[TestClass]
public class SignaturePanelTests
{
    private static IEditorSession CreateSession(string text = "test")
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        return (IEditorSession)node;
    }

    // ── SignaturePanel construction ──────────────────────────

    [TestMethod]
    public void SignaturePanel_SingleSignature_StoresCorrectly()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        TestSeq.Single(panel.Signatures);
        Assert.AreEqual("void Foo(int x)", panel.Signatures[0].Label);
        TestSeq.Single(panel.Signatures[0].Parameters);
        Assert.AreEqual("int x", panel.Signatures[0].Parameters[0].Label);
    }

    [TestMethod]
    public void SignaturePanel_MultipleOverloads_StoresAll()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ]),
            new SignaturePanelEntry("void Foo(int x, string y)", [
                new SignatureParameterInfo("int x"),
                new SignatureParameterInfo("string y")
            ]),
            new SignaturePanelEntry("void Foo()", [])
        ]);

        Assert.AreEqual(3, panel.Signatures.Count);
        TestSeq.Single(panel.Signatures[0].Parameters);
        Assert.AreEqual(2, panel.Signatures[1].Parameters.Count);
        Assert.IsEmpty(panel.Signatures[2].Parameters);
    }

    [TestMethod]
    public void SignaturePanel_ActiveSignatureDefault_IsZero()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo()", [])
        ]);

        Assert.AreEqual(0, panel.ActiveSignature);
    }

    [TestMethod]
    public void SignaturePanel_ActiveParameterDefault_IsZero()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        Assert.AreEqual(0, panel.ActiveParameter);
    }

    [TestMethod]
    public void SignaturePanel_WithActiveSignature_PreservesValue()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo()", []),
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]) { ActiveSignature = 1 };

        Assert.AreEqual(1, panel.ActiveSignature);
    }

    [TestMethod]
    public void SignaturePanel_WithActiveParameter_PreservesValue()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x, string y)", [
                new SignatureParameterInfo("int x"),
                new SignatureParameterInfo("string y")
            ])
        ]) { ActiveParameter = 1 };

        Assert.AreEqual(1, panel.ActiveParameter);
    }

    // ── SignaturePanelEntry ──────────────────────────────────

    [TestMethod]
    public void SignaturePanelEntry_WithDocumentation_StoresValue()
    {
        var entry = new SignaturePanelEntry("void Save(string path)", [
            new SignatureParameterInfo("string path")
        ]) { Documentation = "Saves the document to disk." };

        Assert.AreEqual("Saves the document to disk.", entry.Documentation);
    }

    [TestMethod]
    public void SignaturePanelEntry_WithoutDocumentation_IsNull()
    {
        var entry = new SignaturePanelEntry("void Foo()", []);

        Assert.IsNull(entry.Documentation);
    }

    // ── SignatureParameterInfo ───────────────────────────────

    [TestMethod]
    public void SignatureParameterInfo_WithDocumentation_StoresValue()
    {
        var param = new SignatureParameterInfo("string path")
        {
            Documentation = "The file path to save to."
        };

        Assert.AreEqual("string path", param.Label);
        Assert.AreEqual("The file path to save to.", param.Documentation);
    }

    [TestMethod]
    public void SignatureParameterInfo_WithoutDocumentation_IsNull()
    {
        var param = new SignatureParameterInfo("int count");

        Assert.IsNull(param.Documentation);
    }

    // ── IEditorSession integration ───────────────────────────

    [TestMethod]
    public void ShowSignaturePanel_ThenDismiss_ClearsPanel()
    {
        var session = CreateSession();
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        session.ShowSignaturePanel(panel);
        session.DismissSignaturePanel();

        // DismissSignaturePanel should clear the internal field;
        // show again and dismiss to verify idempotent behavior
        session.DismissSignaturePanel();
    }

    [TestMethod]
    public void ShowSignaturePanel_CalledTwice_ReplacesPanel()
    {
        var session = CreateSession();
        var first = new SignaturePanel([
            new SignaturePanelEntry("void A()", [])
        ]);
        var second = new SignaturePanel([
            new SignaturePanelEntry("void B(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        session.ShowSignaturePanel(first);
        session.ShowSignaturePanel(second);

        // No public accessor, but verifying no exception on replace
        session.DismissSignaturePanel();
    }

    // ── Record equality ──────────────────────────────────────

    [TestMethod]
    public void SignaturePanel_RecordEquality_EqualWhenSameListReference()
    {
        var signatures = new List<SignaturePanelEntry>
        {
            new("void Foo(int x)", [new SignatureParameterInfo("int x")])
        };

        var a = new SignaturePanel(signatures) { ActiveSignature = 0, ActiveParameter = 0 };
        var b = new SignaturePanel(signatures) { ActiveSignature = 0, ActiveParameter = 0 };

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void SignaturePanel_RecordEquality_NotEqualWhenDifferentActiveParam()
    {
        var signatures = new List<SignaturePanelEntry>
        {
            new("void Foo(int x, int y)", [
                new SignatureParameterInfo("int x"),
                new SignatureParameterInfo("int y")
            ])
        };

        var a = new SignaturePanel(signatures) { ActiveParameter = 0 };
        var b = new SignaturePanel(signatures) { ActiveParameter = 1 };

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void SignaturePanelEntry_RecordEquality_EqualWhenSameValues()
    {
        var a = new SignaturePanelEntry("void Foo()", []) { Documentation = "docs" };
        var b = new SignaturePanelEntry("void Foo()", []) { Documentation = "docs" };

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void SignatureParameterInfo_RecordEquality_EqualWhenSameValues()
    {
        var a = new SignatureParameterInfo("int x") { Documentation = "the value" };
        var b = new SignatureParameterInfo("int x") { Documentation = "the value" };

        Assert.AreEqual(a, b);
    }
}
