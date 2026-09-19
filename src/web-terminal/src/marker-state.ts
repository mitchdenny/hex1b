import type { TerminalCommandMark } from "./types.js";
import type { TerminalMarker, TerminalMarkerOptions, MarkerResult } from "./scrollbar-types.js";
import type { HistoryMetadata, TerminalCommand } from "./wire-types.js";
import { randomId } from "./random-id.js";

type MarkerCommand = Extract<TerminalCommand, { type: "marker" }>;
interface Request {
  command: MarkerCommand;
  resolve: (result: MarkerResult) => void;
  reject: (error: Error) => void;
  timer?: ReturnType<typeof setTimeout>;
}

/** One RPC in flight lets a coalesced state frame acknowledge each operation without losing replies. */
export class MarkerState {
  #send: (command: TerminalCommand) => void;
  #change: () => void;
  #markers: readonly TerminalMarker[] = Object.freeze([]);
  #presentation = new Map<string, Pick<TerminalMarker, "label" | "color">>();
  #requests: Request[] = [];
  #serial = 0;
  #revision = 0;
  #presented = false;

  constructor(send: (command: TerminalCommand) => void, change: () => void) {
    this.#send = send;
    this.#change = change;
  }

  get markers(): readonly TerminalMarker[] { return this.#markers; }

  accept(history: HistoryMetadata | null, revision: number): void {
    if (revision <= this.#revision) return;
    this.#revision = revision;
    const next = (history?.markers ?? []).map(marker =>
      Object.freeze({ ...marker, ...this.#presentation.get(marker.id) }));
    const changed = !this.#presented || JSON.stringify(next) !== JSON.stringify(this.#markers);
    this.#presented = true;
    this.#markers = Object.freeze(next);
    const result = history?.markerResult;
    const request = this.#requests[0];
    if (result && request && result.requestId === request.command.requestId) {
      this.#requests.shift();
      clearTimeout(request.timer);
      if (result.success) request.resolve(result);
      else request.reject(new Error(result.error || "Marker operation failed"));
      this.#start();
    }
    const retained = new Set(next.map(marker => marker.id));
    for (const pending of this.#requests)
      if (pending.command.action === "add") retained.add(pending.command.id);
    for (const id of this.#presentation.keys())
      if (!retained.has(id)) this.#presentation.delete(id);
    if (changed) this.#change();
  }

  #request(command: Omit<MarkerCommand, "type" | "requestId">): Promise<MarkerResult> {
    const result = Promise.withResolvers<MarkerResult>();
    this.#requests.push({ command: { type: "marker", ...command, requestId: ++this.#serial },
      resolve: result.resolve, reject: result.reject });
    if (this.#requests.length === 1) this.#start();
    return result.promise;
  }

  #start(): void {
    const next = this.#requests[0];
    if (!next) return;
    try {
      this.#send(next.command);
      next.timer = setTimeout(() => {
        // The server may still process this request; do not issue further dependent operations.
        this.#rejectRequests(new Error("Timed out waiting for the marker operation; resync before retrying."));
      }, 10000);
    } catch (error) {
      this.#requests.shift();
      next.reject(error instanceof Error ? error : new Error(String(error)));
      this.#start();
    }
  }

  async add(options: TerminalMarkerOptions): Promise<TerminalMarker> {
    const position = options?.position;
    if (!position || !validIdentity(position.generation) || !validIdentity(position.rowId) ||
        !Number.isInteger(position.column) || position.column < 0 || position.column > 1024)
      throw new TypeError("A marker requires a valid presented generation, row ID, and column");
    if (options.label !== undefined && (typeof options.label !== "string" || options.label.length > 4096))
      throw new TypeError("Marker label must be at most 4096 characters");
    if (options.color !== undefined && (typeof options.color !== "string" || options.color.length > 256 ||
        !CSS.supports("color", options.color))) throw new TypeError("Marker color must be a CSS color");
    const id = `custom:${randomId()}`;
    this.#presentation.set(id, { ...(options.label === undefined ? {} : { label: options.label }),
      ...(options.color === undefined ? {} : { color: options.color }) });
    try {
      await this.#request({ action: "add", id, ...position });
      const marker = this.#markers.find(item => item.id === id);
      if (!marker) throw new Error("Marker registration succeeded without a retained marker");
      return marker;
    } catch (error) {
      this.#presentation.delete(id);
      throw error;
    }
  }

  async remove(id: string): Promise<void> {
    validateMarkerId(id);
    await this.#request({ action: "remove", id });
    this.#presentation.delete(id);
  }

  async jump(id: string): Promise<void> {
    validateMarkerId(id);
    await this.#request({ action: "jump", id });
  }

  async details(id: string): Promise<TerminalCommandMark> {
    validateMarkerId(id);
    const result = await this.#request({ action: "details", id });
    if (!result.details) throw new Error("Command marker details were not returned");
    return Object.freeze({ ...result.details });
  }

  disconnect(error = new Error("Terminal disconnected before the marker operation completed.")): void {
    this.#rejectRequests(error);
    this.#presentation.clear();
  }

  #rejectRequests(error: Error): void {
    for (const request of this.#requests.splice(0)) {
      clearTimeout(request.timer);
      request.reject(error);
    }
  }
}

function validIdentity(value: unknown): value is string {
  return typeof value === "string" && /^[1-9][0-9]{0,18}$/u.test(value) && BigInt(value) <= 9223372036854775807n;
}

export function validateMarkerId(id: string): void {
  if (typeof id !== "string" || !/^(?:custom|command):[A-Za-z0-9-]{1,100}$/u.test(id))
    throw new TypeError("Invalid terminal marker ID");
}
