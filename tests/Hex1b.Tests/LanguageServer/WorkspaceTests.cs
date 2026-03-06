using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// Tests for <see cref="Hex1bLanguageServerWorkspace"/> — server sharing,
/// provider caching, and language inference.
/// </summary>
public class WorkspaceTests
{
    [Fact]
    public void GetProvider_ReturnsSameProviderForSameUri()
    {
        var server = new TestLanguageServer();
        server.Start();

        var workspace = new Hex1bLanguageServerWorkspace("/test");
        workspace.RegisterServer("csharp", lsp =>
            lsp.WithTransport(server.ClientInput, server.ClientOutput));

        var provider1 = workspace.GetProvider("file:///test/File.cs", "csharp");
        var provider2 = workspace.GetProvider("file:///test/File.cs", "csharp");

        Assert.Same(provider1, provider2);
    }

    [Fact]
    public void GetProvider_ReturnsDifferentProvidersForDifferentUris()
    {
        var server = new TestLanguageServer();
        server.Start();

        var workspace = new Hex1bLanguageServerWorkspace("/test");
        workspace.RegisterServer("csharp", lsp =>
            lsp.WithTransport(server.ClientInput, server.ClientOutput));

        var provider1 = workspace.GetProvider("file:///test/A.cs", "csharp");
        var provider2 = workspace.GetProvider("file:///test/B.cs", "csharp");

        Assert.NotSame(provider1, provider2);
    }

    [Fact]
    public void GetProvider_InfersLanguageFromExtension()
    {
        var server = new TestLanguageServer();
        server.Start();

        var workspace = new Hex1bLanguageServerWorkspace("/test");
        workspace.RegisterServer("csharp", lsp =>
            lsp.WithTransport(server.ClientInput, server.ClientOutput));

        // .cs should infer "csharp"
        var provider = workspace.GetProvider("file:///test/File.cs");
        Assert.NotNull(provider);
    }

    [Fact]
    public void GetProvider_ThrowsForUnregisteredLanguage()
    {
        var workspace = new Hex1bLanguageServerWorkspace("/test");

        Assert.Throws<InvalidOperationException>(() =>
            workspace.GetProvider("file:///test/file.rs", "rust"));
    }
}
