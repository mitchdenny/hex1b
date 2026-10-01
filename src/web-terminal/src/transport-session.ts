import { LIMITS } from "./protocol.js";
import type { TerminalTransport, TerminalTransportCloseDetails, TerminalTransportConnection } from "./transport-types.js";
import { errorMessage } from "./validation.js";

interface Callbacks {
  onReady(): void;
  onFrame(frame: ArrayBuffer): Promise<void>;
  onClose(details: TerminalTransportCloseDetails): void;
  onError(error: Error): void;
}

/** Shared lifecycle and bounded delivery for worker-local and host-bridged transports. */
export class TransportSession {
  #abort = new AbortController();
  #ready = Promise.withResolvers<void>();
  #connection: TerminalTransportConnection | undefined;
  #frame = false;
  #sending = false;
  #queue: { control: string; bytes: number; resolve(): void; reject(error: unknown): void }[] = [];
  #bytes = 0;

  constructor(private callbacks: Callbacks) {
    // Disposal can precede the first frame waiting for readiness.
    void this.#ready.promise.catch(() => {});
  }

  start(transport: TerminalTransport): void {
    void this.#connect(transport);
  }

  async #connect(transport: TerminalTransport): Promise<void> {
    try {
      const connection = await transport.connect({
        signal: this.#abort.signal,
        onFrame: frame => this.#receive(frame),
        onClose: details => {
          if (this.#abort.signal.aborted) return;
          if (!details || typeof details.reason !== "string" ||
              (details.code !== undefined && (!Number.isInteger(details.code) || typeof details.wasClean !== "boolean"))) {
            this.#fail(new TypeError("Invalid terminal transport close details"));
            return;
          }
          const snapshot = details.code === undefined ? { reason: details.reason }
            : { code: details.code, reason: details.reason, wasClean: details.wasClean };
          this.dispose();
          this.callbacks.onClose(snapshot);
        },
        onError: error => this.#fail(error)
      });
      if (!connection || typeof connection.send !== "function" || typeof connection.dispose !== "function")
        throw new TypeError("transport.connect must return a connection with send and dispose");
      if (this.#abort.signal.aborted) { connection.dispose(); return; }
      this.#connection = connection;
      this.callbacks.onReady();
      this.#ready.resolve();
    } catch (error) { this.#fail(error); }
  }

  async #receive(frame: ArrayBuffer | Uint8Array<ArrayBuffer>): Promise<void> {
    try {
      this.#abort.signal.throwIfAborted();
      if (this.#frame) throw new Error("Concurrent terminal transport frames are not allowed");
      if (!(frame instanceof ArrayBuffer) &&
          !(frame instanceof Uint8Array && frame.buffer instanceof ArrayBuffer))
        throw new TypeError("Transport frames must be ArrayBuffer or Uint8Array backed by ArrayBuffer");
      if (!frame.byteLength || frame.byteLength > LIMITS.frameBytes)
        throw new RangeError("Invalid HWT1 frame size");
      this.#frame = true;
      const buffer = frame instanceof ArrayBuffer ? frame : new Uint8Array(frame).buffer;
      await this.#ready.promise;
      this.#abort.signal.throwIfAborted();
      await this.callbacks.onFrame(buffer);
      this.#abort.signal.throwIfAborted();
      this.#frame = false;
    } catch (error) {
      this.#fail(error);
      throw error;
    }
  }

  send(control: string): Promise<void> {
    if (this.#abort.signal.aborted) return Promise.reject(this.#abort.signal.reason);
    const bytes = new TextEncoder().encode(control).byteLength;
    if (this.#queue.length >= 256 || this.#bytes + bytes > 1024 * 1024) {
      const error = new Error("Terminal transport control queue exceeded its bounded capacity");
      this.#fail(error);
      return Promise.reject(error);
    }
    const pending = Promise.withResolvers<void>();
    this.#queue.push({ control, bytes, resolve: pending.resolve, reject: pending.reject });
    this.#bytes += bytes;
    void this.#drain();
    return pending.promise;
  }

  async #drain(): Promise<void> {
    if (this.#sending) return;
    this.#sending = true;
    try {
      await this.#ready.promise;
      while (!this.#abort.signal.aborted && this.#queue.length) {
        const item = this.#queue[0];
        await this.#connection!.send(item.control);
        if (this.#abort.signal.aborted) return;
        this.#queue.shift();
        this.#bytes -= item.bytes;
        item.resolve();
      }
    } catch (error) { this.#fail(error); }
    finally { this.#sending = false; }
  }

  #fail(error: unknown): void {
    if (this.#abort.signal.aborted) return;
    const failure = error instanceof Error ? error : new Error(errorMessage(error));
    this.#stop(failure);
    this.callbacks.onError(failure);
  }

  dispose(): void {
    this.#stop(new DOMException("Terminal transport was disposed or closed", "AbortError"));
  }

  #stop(error: Error): void {
    if (this.#abort.signal.aborted) return;
    this.#abort.abort(error);
    this.#ready.reject(error);
    for (const item of this.#queue) item.reject(error);
    this.#queue = [];
    this.#bytes = 0;
    this.#connection?.dispose();
    this.#connection = undefined;
  }
}
