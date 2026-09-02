# Authenticated Remote Terminal Demo

This sample has separate server and client applications. The server hosts a Hex1b application at a WebSocket attach endpoint and rejects requests that do not include a non-empty bearer token. The client supplies the token through the `ClientWebSocketOptions` callback on `WithRemoteTerminal`.

The token check intentionally validates only the presence and shape of the `Authorization` header. Replace it with normal JWT bearer authentication before using this pattern outside a local demonstration.

## Run the server

```bash
dotnet run --project samples/RemoteTerminalAuthDemo/Server
```

The server listens at `ws://localhost:5050/ws/attach`.

You can verify that an unauthenticated request is rejected:

```bash
curl -i http://localhost:5050/ws/attach
```

## Run the client

In another terminal:

```bash
dotnet run --project samples/RemoteTerminalAuthDemo/Client
```

The client uses `demo-token` by default. You can pass a different endpoint and token as positional arguments:

```bash
dotnet run --project samples/RemoteTerminalAuthDemo/Client -- \
  ws://localhost:5050/ws/attach \
  my-token
```

The important client configuration is:

```csharp
.WithRemoteTerminal(
    endpoint,
    options => options.SetRequestHeader(
        "Authorization",
        $"Bearer {bearerToken}"))
```

The server parses the `Authorization` header and returns HTTP 401 unless its scheme is `Bearer` and its token is non-empty. After accepting the WebSocket, it bridges the public `AttachSession` protocol to the hosted Hex1b application.
