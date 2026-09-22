import type { TerminalTransport, TerminalTransportConnection } from "./transport-types.js";

const websocketUrls = new WeakMap<TerminalTransport, string>();

function normalizeUrl(value: string): string {
  const url = new URL(value, globalThis.location?.href);
  if (url.protocol === "https:") url.protocol = "wss:";
  if (url.protocol === "http:") url.protocol = "ws:";
  if (!["ws:", "wss:"].includes(url.protocol)) throw new TypeError("A ws: or wss: URL is required");
  return url.href;
}

/** Creates the first-party live transport. Mount runs this transport in its rendering worker. */
export function createWebSocketTransport(url: string | URL): TerminalTransport {
  if (!(url instanceof URL) && (typeof url !== "string" || !url.trim()))
    throw new TypeError("A terminal WebSocket URL is required");
  const value = String(url);
  const transport: TerminalTransport = {
    connect(context) {
      return new Promise<TerminalTransportConnection>((resolve, reject) => {
        context.signal.throwIfAborted();
        const socket = new WebSocket(normalizeUrl(value));
        socket.binaryType = "arraybuffer";
        const abort = () => {
          socket.close(1000, "View detached");
          reject(context.signal.reason);
        };
        context.signal.addEventListener("abort", abort, { once: true });
        socket.addEventListener("open", () => {
          if (context.signal.aborted) return;
          resolve({
            send(control) {
              if (socket.readyState !== WebSocket.OPEN) throw new Error("Terminal WebSocket is not open");
              if (socket.bufferedAmount + new TextEncoder().encode(control).byteLength > 1024 * 1024)
                throw new Error("Terminal WebSocket control buffer exceeded 1 MiB");
              socket.send(control);
            },
            dispose() {
              context.signal.removeEventListener("abort", abort);
              socket.close(1000, "View detached");
            }
          });
        });
        socket.addEventListener("message", event => {
          if (context.signal.aborted) return;
          if (!(event.data instanceof ArrayBuffer)) {
            context.onError(new Error("Expected binary HWT1 frame"));
            return;
          }
          void context.onFrame(event.data).catch(error => context.onError(error));
        });
        // Browser errors are followed by close, which carries the actual native status.
        socket.addEventListener("close", event => {
          context.signal.removeEventListener("abort", abort);
          if (context.signal.aborted) return;
          context.onClose({ code: event.code, reason: event.reason, wasClean: event.wasClean });
          reject(new Error(`Terminal WebSocket closed (${event.code})`));
        });
      });
    }
  };
  websocketUrls.set(transport, value);
  return Object.freeze(transport);
}

export function workerWebSocketUrl(transport: TerminalTransport): string | undefined {
  const url = websocketUrls.get(transport);
  return url === undefined ? undefined : normalizeUrl(url);
}

export function selectTransport(options: { url?: string | URL; transport?: TerminalTransport }): TerminalTransport {
  if (!options || (options.url !== undefined) === (options.transport !== undefined))
    throw new TypeError("Exactly one of url or transport is required");
  if (options.url !== undefined) return createWebSocketTransport(options.url);
  if (!options.transport || typeof options.transport.connect !== "function")
    throw new TypeError("transport must provide connect(context)");
  return options.transport;
}
