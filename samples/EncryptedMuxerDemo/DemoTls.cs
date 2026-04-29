using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EncryptedMuxerDemo;

/// <summary>
/// Provides TLS stream wrapping helpers for the encrypted muxer demo.
/// Generates an ephemeral self-signed certificate at startup and provides
/// server/client stream transform functions for use with HMP v1 builder extensions.
/// </summary>
internal static class DemoTls
{
    private static X509Certificate2? _serverCert;

    /// <summary>
    /// Gets (or lazily creates) an ephemeral self-signed certificate for the demo server.
    /// The certificate is generated in-memory and never persisted to disk.
    /// </summary>
    public static X509Certificate2 ServerCertificate => _serverCert ??= GenerateSelfSignedCert();

    /// <summary>
    /// Wraps a raw transport stream with TLS as the server side.
    /// Performs the TLS handshake asynchronously before returning the encrypted stream.
    /// </summary>
    public static async Task<Stream> AuthenticateAsServerAsync(Stream innerStream)
    {
        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(ServerCertificate);
        return sslStream;
    }

    /// <summary>
    /// Wraps a raw transport stream with TLS as the client side.
    /// Uses a permissive certificate validation callback since the server
    /// certificate is ephemeral and self-signed.
    /// </summary>
    public static async Task<Stream> AuthenticateAsClientAsync(Stream innerStream)
    {
        var sslStream = new SslStream(
            innerStream,
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, _, _, _) => true);

        await sslStream.AuthenticateAsClientAsync("hex1b-encrypted-muxer-demo");
        return sslStream;
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=hex1b-encrypted-muxer-demo",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], // serverAuth
                critical: false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));

        // Export to PFX and re-import so the private key is in a format SslStream can use.
        // Use MachineKeySet to avoid "ephemeral keys not supported" errors on some Windows versions.
        var pfxBytes = cert.Export(X509ContentType.Pfx);
        cert.Dispose();
        return X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.MachineKeySet);
    }
}
