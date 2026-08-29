using System.Security.Cryptography;

namespace Hex1b.Tests;

[TestClass]
public class WebMuxerVendoredXtermTests
{
    [TestMethod]
    public void WebMuxerDemo_VendoredBundleContainsTombstoneFixAndProvenance()
    {
        var root = FindRepositoryRoot();
        var demo = Path.Combine(root, "samples", "WebMuxerDemo");
        var vendor = Path.Combine(demo, "wwwroot", "vendor");
        var expectedHashes = new Dictionary<string, string>
        {
            ["xterm.js"] =
                "ce4cc36d5e30951cca5c6849de5a1465ab9a30fb5504cbe84a9a26579cc5d591",
            ["xterm.css"] =
                "4d9a1d50808997f097ccc6040a5da6f6cb06b14e5ee2402df5196a218bba838f",
            ["xterm-addon-fit.js"] =
                "acc70dbdb41ff8e6cb8b37ad888dabd2285267e97f79f2e72ab864fd6337df1c",
            ["xterm-addon-image.js"] =
                "bfcbf94862d6909cbbb3bb0e0fd0ec821c88317a5c7acac9bc1c67209d2dafe3",
            ["xterm.LICENSE"] =
                "b569f629d00f2626a8100df2a1798210535621e42164dfd426a6fe5aac7b0ccd",
            ["LICENSE.addon-fit.txt"] =
                "e256f01188af527e4d06d21d06fbf785ae9c50d4b328bf03cbe0ba7f0aa4228f",
            ["LICENSE.addon-image.txt"] =
                "00994f891153ba5d613e2b838a9ad2f47d4484070cb594b46efc547ebea5ca05",
            ["addon-image.js.LICENSE.txt"] =
                "0d5cae8c21a98d6eb5aa651d1ba2f41ba270a1caffa78645e5c25dee80370729",
        };

        foreach (var (name, expectedHash) in expectedHashes)
        {
            var bytes = File.ReadAllBytes(Path.Combine(vendor, name));
            var actualHash = Convert
                .ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant();
            Assert.AreEqual(expectedHash, actualHash, name);
        }

        var addon = File.ReadAllText(
            Path.Combine(vendor, "xterm-addon-image.js"));
        Assert.Contains("_rebuildImageCellIndex", addon);
        Assert.Contains(
            "this._rebuildImageCellIndex(A);const s=this._clearImageCells(A)",
            addon);
        Assert.Contains("reconcileImageCellIndexes", addon);
        Assert.Contains("hasClientId", addon);
        Assert.Contains("releaseUnreferencedImage", addon);

        var provenance = File.ReadAllText(
            Path.Combine(vendor, "README.md"));
        Assert.Contains(
            "825f5605c9afc14cc1f7579959a9699c23df3f0f",
            provenance);
        foreach (var expectedHash in expectedHashes.Values)
            Assert.Contains(expectedHash, provenance);

        var index = File.ReadAllText(
            Path.Combine(demo, "wwwroot", "index.html"));
        Assert.Contains("/vendor/xterm.js?v=825f5605", index);
        Assert.Contains("/vendor/xterm.css?v=825f5605", index);
        Assert.Contains("/vendor/xterm-addon-fit.js?v=825f5605", index);
        Assert.Contains("/vendor/xterm-addon-image.js?v=825f5605", index);

        var program = File.ReadAllText(Path.Combine(demo, "Program.cs"));
        Assert.Contains("app.UseStaticFiles();", program);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName,
                    "samples",
                    "WebMuxerDemo",
                    "wwwroot",
                    "vendor")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find the Hex1b repository root.");
    }
}
