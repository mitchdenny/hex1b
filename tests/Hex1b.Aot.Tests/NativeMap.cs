using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Hex1b.Aot.Tests;

internal sealed record MethodCode(string Name, long Length);
internal sealed record CodeFamily(string Name, string Pattern);
internal sealed record CodeEvidence(string Family, bool Required, int Count, long Length);

internal static class NativeMap
{
    public static IReadOnlyList<MethodCode> Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static IReadOnlyList<MethodCode> Read(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var document = XDocument.Load(reader);
        if (document.Root?.Name != "ObjectNodes")
            throw new InvalidDataException("Unsupported native compiler map: expected ObjectNodes root.");
        var methods = new List<MethodCode>();
        foreach (var node in document.Root.Elements("MethodCode"))
        {
            var name = (string?)node.Attribute("Name");
            if (string.IsNullOrWhiteSpace(name) ||
                !long.TryParse((string?)node.Attribute("Length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length) ||
                length <= 0)
                throw new InvalidDataException("Native MethodCode requires a nonempty Name and a positive integer Length.");
            methods.Add(new(name, length));
        }
        if (methods.Count == 0)
            throw new InvalidDataException("Native compiler map contains no emitted MethodCode entries.");
        return methods;
    }

    public static IReadOnlyList<CodeEvidence> Measure(IReadOnlyList<MethodCode> methods,
        IReadOnlyList<CodeFamily> required, IReadOnlyList<CodeFamily> forbidden)
        => required.Select(family => MeasureFamily(family, true, methods))
            .Concat(forbidden.Select(family => MeasureFamily(family, false, methods))).ToArray();

    public static void Validate(IReadOnlyList<CodeEvidence> evidence)
    {
        var failures = evidence.Where(e => e.Required ? e.Count == 0 : e.Count != 0).ToArray();
        if (failures.Length != 0)
            throw new InvalidDataException("Incorrect emitted MethodCode retention: " + string.Join("; ",
                failures.Select(e => $"{e.Family}: {(e.Required ? "required" : "forbidden")}, count={e.Count}, length={e.Length}")));
    }

    private static CodeEvidence MeasureFamily(CodeFamily family, bool required, IReadOnlyList<MethodCode> methods)
    {
        var matcher = new Regex(family.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var matching = methods.Where(method => matcher.IsMatch(method.Name)).ToArray();
        return new(family.Name, required, matching.Length, matching.Sum(method => method.Length));
    }
}
