using System.Net.Http;

namespace Hex1b;

internal sealed class RemoteTerminalRequestHandler(
    Action<HttpRequestMessage>? configureRequest) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        configureRequest?.Invoke(request);
        return base.SendAsync(request, cancellationToken);
    }
}
