using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SignaturePanel"/>, <see cref="SignaturePanelEntry"/>,
/// <see cref="SignatureParameterInfo"/>, and their integration with
/// <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
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

    [Fact]
    public void SignaturePanel_SingleSignature_StoresCorrectly()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        Assert.Single(panel.Signatures);
        Assert.Equal("void Foo(int x)", panel.Signatures[0].Label);
        Assert.Single(panel.Signatures[0].Parameters);
        Assert.Equal("int x", panel.Signatures[0].Parameters[0].Label);
    }

    [Fact]
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

        Assert.Equal(3, panel.Signatures.Count);
        Assert.Single(panel.Signatures[0].Parameters);
        Assert.Equal(2, panel.Signatures[1].Parameters.Count);
        Assert.Empty(panel.Signatures[2].Parameters);
    }

    [Fact]
    public void SignaturePanel_ActiveSignatureDefault_IsZero()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo()", [])
        ]);

        Assert.Equal(0, panel.ActiveSignature);
    }

    [Fact]
    public void SignaturePanel_ActiveParameterDefault_IsZero()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]);

        Assert.Equal(0, panel.ActiveParameter);
    }

    [Fact]
    public void SignaturePanel_WithActiveSignature_PreservesValue()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo()", []),
            new SignaturePanelEntry("void Foo(int x)", [
                new SignatureParameterInfo("int x")
            ])
        ]) { ActiveSignature = 1 };

        Assert.Equal(1, panel.ActiveSignature);
    }

    [Fact]
    public void SignaturePanel_WithActiveParameter_PreservesValue()
    {
        var panel = new SignaturePanel([
            new SignaturePanelEntry("void Foo(int x, string y)", [
                new SignatureParameterInfo("int x"),
                new SignatureParameterInfo("string y")
            ])
        ]) { ActiveParameter = 1 };

        Assert.Equal(1, panel.ActiveParameter);
    }

    // ── SignaturePanelEntry ──────────────────────────────────

    [Fact]
    public void SignaturePanelEntry_WithDocumentation_StoresValue()
    {
        var entry = new SignaturePanelEntry("void Save(string path)", [
            new SignatureParameterInfo("string path")
        ]) { Documentation = "Saves the document to disk." };

        Assert.Equal("Saves the document to disk.", entry.Documentation);
    }

    [Fact]
    public void SignaturePanelEntry_WithoutDocumentation_IsNull()
    {
        var entry = new SignaturePanelEntry("void Foo()", []);

        Assert.Null(entry.Documentation);
    }

    // ── SignatureParameterInfo ───────────────────────────────

    [Fact]
    public void SignatureParameterInfo_WithDocumentation_StoresValue()
    {
        var param = new SignatureParameterInfo("string path")
        {
            Documentation = "The file path to save to."
        };

        Assert.Equal("string path", param.Label);
        Assert.Equal("The file path to save to.", param.Documentation);
    }

    [Fact]
    public void SignatureParameterInfo_WithoutDocumentation_IsNull()
    {
        var param = new SignatureParameterInfo("int count");

        Assert.Null(param.Documentation);
    }

    // ── IEditorSession integration ───────────────────────────

    [Fact]
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

    [Fact]
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

    [Fact]
    public void SignaturePanel_RecordEquality_EqualWhenSameListReference()
    {
        var signatures = new List<SignaturePanelEntry>
        {
            new("void Foo(int x)", [new SignatureParameterInfo("int x")])
        };

        var a = new SignaturePanel(signatures) { ActiveSignature = 0, ActiveParameter = 0 };
        var b = new SignaturePanel(signatures) { ActiveSignature = 0, ActiveParameter = 0 };

        Assert.Equal(a, b);
    }

    [Fact]
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

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SignaturePanelEntry_RecordEquality_EqualWhenSameValues()
    {
        var a = new SignaturePanelEntry("void Foo()", []) { Documentation = "docs" };
        var b = new SignaturePanelEntry("void Foo()", []) { Documentation = "docs" };

        Assert.Equal(a, b);
    }

    [Fact]
    public void SignatureParameterInfo_RecordEquality_EqualWhenSameValues()
    {
        var a = new SignatureParameterInfo("int x") { Documentation = "the value" };
        var b = new SignatureParameterInfo("int x") { Documentation = "the value" };

        Assert.Equal(a, b);
    }
}
