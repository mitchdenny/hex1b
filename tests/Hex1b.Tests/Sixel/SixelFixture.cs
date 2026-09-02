using System.Reflection;
using System.Text;

namespace Hex1b.Tests.Sixel;

internal sealed record SixelFixture(
    string Name,
    string Description,
    byte[] Payload)
{
    public byte[] StandardBytes =>
    [
        0x1b,
        (byte)'P',
        .. Payload,
        0x1b,
        (byte)'\\',
    ];

    public byte[] C1Bytes =>
    [
        0x90,
        .. Payload,
        0x9c,
    ];

    public static SixelFixture Load(string name, string description)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var suffix = $".TestData.Sixel.{name}.sixel";
        var resourceName = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Sixel fixture '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var payload = reader.ReadToEnd().TrimEnd('\r', '\n');
        return new SixelFixture(name, description, Encoding.ASCII.GetBytes(payload));
    }
}
