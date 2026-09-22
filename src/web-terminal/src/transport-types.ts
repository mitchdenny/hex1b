import type { TerminalCloseDetails } from "./types.js";

/** A transport close is not workload completion. Only WebSocket supplies code/wasClean. */
export type TerminalTransportCloseDetails = TerminalCloseDetails | {
  readonly reason: string;
  readonly code?: never;
  readonly wasClean?: never;
};

/** A ready, per-view connection. Detaching must not terminate the shared terminal. */
export interface TerminalTransportConnection {
  /**
   * Forwards one opaque serialized HWT1 control unchanged. Calls are serialized:
   * resolve only when the control has been accepted in order by the underlying channel.
   * Throw/reject on failure. Do not wait for a subsequent frame or presentation ACK.
   */
  send(control: string): void | Promise<void>;
  /** Idempotently detach listeners and initiate connection teardown, without throwing. */
  dispose(): void;
}

/** Listeners are installed before connect is called and remain valid until signal aborts. */
export interface TerminalTransportContext {
  /** Aborted on disposal, close, failure, or mount cancellation, including during connect. */
  readonly signal: AbortSignal;
  /**
   * Delivers one complete ordered binary HWT1 frame. An ArrayBuffer transfers ownership
   * immediately: never read/reuse it afterwards. Uint8Array slices are copied synchronously.
   * Await completion before delivering another frame; concurrent deliveries fail the view.
   * Completion means worker acceptance, NOT presentation. Forward all opaque outgoing
   * controls independently; the producer must still obey HWT1's one-unacknowledged-frame gate.
   * May be called before connect resolves (one frame waits for readiness).
   */
  onFrame(frame: ArrayBuffer | Uint8Array<ArrayBuffer>): Promise<void>;
  /** Reports an actual channel close, including before connect resolves. */
  onClose(details: TerminalTransportCloseDetails): void;
  /** Reports a fatal channel error. It does not manufacture a close notification. */
  onError(error: Error): void;
}

/** Framework-neutral live HWT1 transport factory, invoked once per mounted view. */
export interface TerminalTransport {
  /**
   * Attach listeners before starting delivery. Return/resolve when controls can be sent.
   * Do not await onFrame inside connect: an early frame waits for this connection.
   * Honor signal during async attachment; Hex1b also disposes any late-returned connection.
   * Each call must create an independent connection. No automatic reconnection is performed.
   */
  connect(context: TerminalTransportContext): TerminalTransportConnection | Promise<TerminalTransportConnection>;
}
