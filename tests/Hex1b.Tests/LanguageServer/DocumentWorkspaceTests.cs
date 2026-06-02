using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// Tests for <see cref="Hex1bDocumentWorkspace"/> — document management,
/// file persistence, dirty tracking, server mapping, and provider resolution.
/// </summary>
[TestClass]
public class DocumentWorkspaceTests : IDisposable
{
    private readonly string _tempDir;

    public DocumentWorkspaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hex1b-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── Document management ──────────────────────────────────

    [TestMethod]
    public async Task OpenDocumentAsync_LoadsFileContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "Hello, World!");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = await workspace.OpenDocumentAsync("test.cs");

        Assert.AreEqual("Hello, World!", doc.GetText());
        Assert.AreEqual(Path.Combine(_tempDir, "test.cs"), doc.FilePath);
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public async Task OpenDocumentAsync_ReturnsSameInstanceForSamePath()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "content");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc1 = await workspace.OpenDocumentAsync("test.cs");
        var doc2 = await workspace.OpenDocumentAsync("test.cs");

        Assert.AreSame(doc1, doc2);
    }

    [TestMethod]
    public async Task CreateDocument_WithPath_SetsFilePath()
    {
        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = workspace.CreateDocument("initial", "new-file.cs");

        Assert.AreEqual(Path.Combine(_tempDir, "new-file.cs"), doc.FilePath);
        Assert.AreEqual("initial", doc.GetText());
    }

    [TestMethod]
    public async Task CreateDocument_WithoutPath_HasNoFilePath()
    {
        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = workspace.CreateDocument("in memory");

        Assert.IsNull(doc.FilePath);
        Assert.IsFalse(doc.IsDirty);
    }

    // ── Dirty tracking ───────────────────────────────────────

    [TestMethod]
    public async Task IsDirty_TrueAfterEdit()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "original");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = await workspace.OpenDocumentAsync("test.cs");

        Assert.IsFalse(doc.IsDirty);

        doc.Apply(new InsertOperation(new DocumentOffset(0), "inserted "));

        Assert.IsTrue(doc.IsDirty);
    }

    [TestMethod]
    public async Task IsDirty_FalseAfterSave()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "original");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = await workspace.OpenDocumentAsync("test.cs");

        doc.Apply(new InsertOperation(new DocumentOffset(0), "inserted "));
        Assert.IsTrue(doc.IsDirty);

        await doc.SaveAsync();
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public async Task SaveAsync_WritesToFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "original");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = await workspace.OpenDocumentAsync("test.cs");

        doc.Apply(new ReplaceOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(8)), "modified"));
        await doc.SaveAsync();

        var saved = File.ReadAllText(Path.Combine(_tempDir, "test.cs"));
        Assert.AreEqual("modified", saved);
    }

    [TestMethod]
    public async Task SaveAsync_ThrowsForInMemoryDocument()
    {
        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = workspace.CreateDocument("no path");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => doc.SaveAsync());
    }

    [TestMethod]
    public async Task SaveAllAsync_SavesDirtyDocuments()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.cs"), "aaa");
        File.WriteAllText(Path.Combine(_tempDir, "b.cs"), "bbb");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var a = await workspace.OpenDocumentAsync("a.cs");
        var b = await workspace.OpenDocumentAsync("b.cs");

        a.Apply(new ReplaceOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(3)), "AAA"));
        // b is not edited

        await workspace.SaveAllAsync();

        Assert.AreEqual("AAA", File.ReadAllText(Path.Combine(_tempDir, "a.cs")));
        Assert.AreEqual("bbb", File.ReadAllText(Path.Combine(_tempDir, "b.cs"))); // unchanged
        Assert.IsFalse(a.IsDirty);
    }

    // ── Language server mapping ──────────────────────────────

    [TestMethod]
    public async Task MapLanguageServer_ThrowsForUnregisteredServer()
    {
        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            workspace.MapLanguageServer("*.cs", "nonexistent"));
    }

    [TestMethod]
    public async Task GetProvider_ResolvesServerByGlob()
    {
        var server = new TestLanguageServer();
        server.Start();

        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "class Foo {}");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        workspace.AddLanguageServer("csharp-ls", lsp =>
            lsp.WithTransport(server.ClientInput, server.ClientOutput)
               .WithLanguageId("csharp"));
        workspace.MapLanguageServer("*.cs", "csharp-ls");

        var doc = await workspace.OpenDocumentAsync("test.cs");
        var provider = workspace.GetProvider(doc);

        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public async Task GetProvider_ReturnsSameProviderForSameDocument()
    {
        var server = new TestLanguageServer();
        server.Start();

        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "class Foo {}");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        workspace.AddLanguageServer("csharp-ls", lsp =>
            lsp.WithTransport(server.ClientInput, server.ClientOutput)
               .WithLanguageId("csharp"));
        workspace.MapLanguageServer("*.cs", "csharp-ls");

        var doc = await workspace.OpenDocumentAsync("test.cs");
        var p1 = workspace.GetProvider(doc);
        var p2 = workspace.GetProvider(doc);

        Assert.AreSame(p1, p2);
    }

    [TestMethod]
    public async Task GetProvider_ReturnsNullForUnmappedExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "hello");

        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        workspace.AddLanguageServer("csharp-ls", lsp =>
            lsp.WithServerCommand("csharp-ls"));
        workspace.MapLanguageServer("*.cs", "csharp-ls");

        var doc = await workspace.OpenDocumentAsync("readme.txt");
        var provider = workspace.GetProvider(doc);

        Assert.IsNull(provider);
    }

    [TestMethod]
    public async Task GetProvider_ReturnsNullForInMemoryDocument()
    {
        await using var workspace = new Hex1bDocumentWorkspace(_tempDir);
        var doc = workspace.CreateDocument("no path");

        var provider = workspace.GetProvider(doc);

        Assert.IsNull(provider);
    }

    // ── Dispose semantics ────────────────────────────────────

    [TestMethod]
    public async Task DisposeAsync_ClearsDocuments()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), "content");

        var workspace = new Hex1bDocumentWorkspace(_tempDir);
        await workspace.OpenDocumentAsync("test.cs");

        TestSeq.Single(workspace.OpenDocuments);

        await workspace.DisposeAsync();

        Assert.IsEmpty(workspace.OpenDocuments);
    }
}
