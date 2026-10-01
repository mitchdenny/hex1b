import { decodeFrame } from "./protocol.js";
import { isRecord } from "./validation.js";
import type { TerminalPlaybackState } from "./types.js";

// Spike container, not an archival contract. Recordings and player must be built together.
export const RECORDING_LIMITS = Object.freeze({
  bytes: 64 * 1024 * 1024,
  frames: 10000,
  durationMs: 10 * 60 * 1000,
});

export interface RecordingFrame { timeMs: number; data: ArrayBuffer }
export interface Recording { durationMs: number; frames: RecordingFrame[] }

export function parseRecording(text: string): Recording {
  if (text.length > RECORDING_LIMITS.bytes || new TextEncoder().encode(text).length > RECORDING_LIMITS.bytes)
    throw new Error("Recording exceeds the 64 MiB spike limit");
  const value: unknown = JSON.parse(text);
  if (!isRecord(value) || value.format !== "hex1b-hwt-recording" || value.version !== 1)
    throw new Error("Unsupported HWT recording format; use a recording from the same player build");
  if (typeof value.durationMs !== "number" || !Number.isFinite(value.durationMs) ||
      value.durationMs < 0 || value.durationMs > RECORDING_LIMITS.durationMs)
    throw new Error("Invalid recording duration");
  if (!Array.isArray(value.frames) || !value.frames.length || value.frames.length > RECORDING_LIMITS.frames)
    throw new Error("Invalid recording frame count");
  const durationMs = value.durationMs;
  let previousTime = 0;
  let revision = 0;
  let columns = 0;
  let rows = 0;
  let colorEncoding: "indexed-v1" | null = null;
  let retained = new Set<string>();
  const frames = value.frames.map((entry: unknown, index): RecordingFrame => {
    if (!isRecord(entry) || typeof entry.timeMs !== "number" || !Number.isFinite(entry.timeMs) ||
        entry.timeMs < previousTime || entry.timeMs > durationMs ||
        (index === 0 && entry.timeMs !== 0))
      throw new Error(`Invalid recording timestamp at frame ${index}`);
    if (typeof entry.data !== "string" || !entry.data.length || entry.data.length % 4 !== 0 ||
        !/^[A-Za-z0-9+/]*={0,2}$/u.test(entry.data))
      throw new Error(`Invalid recording frame encoding at frame ${index}`);
    const bytes = Uint8Array.from(atob(entry.data), character => character.charCodeAt(0));
    const frame = decodeFrame(bytes.buffer);
    const metadata = frame.metadata;
    if (index === 0 && !metadata.full)
      throw new Error("Recording must start with a full HWT1 frame");
    if (!metadata.full && (metadata.baseRevision !== revision || metadata.revision <= revision ||
        metadata.columns !== columns || metadata.rows !== rows ||
        (metadata.colorEncoding ?? null) !== colorEncoding))
      throw new Error(`Broken recording delta chain at frame ${index}`);
    if (metadata.full) retained.clear();
    for (const image of frame.images) retained.add(image.key);
    for (const key of metadata.retainedImages) {
      if (!retained.has(key)) throw new Error(`Missing recording image resource at frame ${index}: ${key}`);
    }
    retained = new Set(metadata.retainedImages);
    revision = metadata.revision;
    columns = metadata.columns;
    rows = metadata.rows;
    colorEncoding = metadata.colorEncoding ?? null;
    previousTime = entry.timeMs;
    return { timeMs: entry.timeMs, data: bytes.buffer };
  });
  return { durationMs, frames };
}

export async function fetchRecording(url: URL, signal: AbortSignal): Promise<Recording> {
  if (!["http:", "https:"].includes(url.protocol)) throw new Error("Recording URL must use HTTP or HTTPS");
  const response = await fetch(url, { signal });
  if (!response.ok) throw new Error(`Could not load recording: HTTP ${response.status}`);
  if (Number(response.headers.get("content-length")) > RECORDING_LIMITS.bytes)
    throw new Error("Recording exceeds the 64 MiB spike limit");
  if (!response.body) throw new Error("Recording response has no body");
  const reader = response.body.getReader();
  const decoder = new TextDecoder("utf-8", { fatal: true });
  let text = "";
  let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > RECORDING_LIMITS.bytes) throw new Error("Recording exceeds the 64 MiB spike limit");
      text += decoder.decode(value, { stream: true });
    }
    text += decoder.decode();
  } finally {
    await reader.cancel();
    reader.releaseLock();
  }
  return parseRecording(text);
}

export interface PlaybackClock {
  now(): number;
  schedule(callback: () => void, delay: number): ReturnType<typeof setTimeout>;
  cancel(timer: ReturnType<typeof setTimeout>): void;
}

/** Replays every delta in order, with one presentation outstanding and a pausable clock. */
export class RecordingPlayer {
  #index = -1;
  #position = 0;
  #started = 0;
  #status: TerminalPlaybackState["status"] = "paused";
  #timer: ReturnType<typeof setTimeout> | undefined;
  #pending: Promise<void> = Promise.resolve();
  #busy = false;
  #disposed = false;
  #restarting = false;

  constructor(
    private readonly recording: Recording,
    private readonly present: (frame: RecordingFrame) => Promise<void>,
    private readonly changed: (state: TerminalPlaybackState) => void,
    private readonly failed: (error: unknown) => void,
    private readonly clock: PlaybackClock = {
      now: () => performance.now(),
      schedule: (callback, delay) => setTimeout(callback, delay),
      cancel: timer => clearTimeout(timer),
    },
  ) {}

  get state(): TerminalPlaybackState {
    return {
      status: this.#status, positionMs: this.#time(), durationMs: this.recording.durationMs,
      frameIndex: this.#index, frameCount: this.recording.frames.length,
    };
  }

  async initialize(): Promise<void> {
    await this.#show(0);
    this.#notify();
  }

  play(): void {
    if (this.#disposed || this.#restarting) throw new Error("Recording is not available for playback");
    if (this.#status === "ended") throw new Error("Restart the recording before playing again");
    if (this.#status === "playing") return;
    this.#started = this.clock.now();
    this.#status = "playing";
    this.#notify();
    this.#schedule();
  }

  pause(): void {
    if (this.#disposed) throw new Error("Recording is disposed");
    if (this.#status !== "playing") return;
    this.#position = this.#time();
    this.#status = "paused";
    this.#cancelTimer();
    if (!this.#busy) this.#notify();
  }

  async restart(): Promise<void> {
    if (this.#disposed) throw new Error("Recording is disposed");
    if (this.#restarting) return;
    this.#restarting = true;
    this.#position = this.#time();
    this.#status = "paused";
    this.#cancelTimer();
    try {
      await this.#pending;
      if (this.#disposed) return;
      this.#position = 0;
      this.#status = "paused";
      await this.#show(0);
      this.#notify();
    } finally {
      this.#restarting = false;
    }
  }

  dispose(): void {
    this.#position = this.#time();
    this.#status = "paused";
    this.#disposed = true;
    this.#cancelTimer();
  }

  #time(): number {
    return Math.min(this.recording.durationMs, this.#position +
      (this.#status === "playing" ? this.clock.now() - this.#started : 0));
  }

  #notify(): void { if (!this.#disposed) this.changed(this.state); }

  #cancelTimer(): void {
    if (this.#timer !== undefined) this.clock.cancel(this.#timer);
    this.#timer = undefined;
  }

  #schedule(): void {
    if (this.#disposed || this.#busy || this.#status !== "playing" || this.#timer !== undefined) return;
    const next = this.recording.frames[this.#index + 1]?.timeMs ?? this.recording.durationMs;
    this.#timer = this.clock.schedule(() => {
      this.#timer = undefined;
      this.#tick().catch(error => {
        this.dispose();
        this.failed(error);
      });
    }, Math.min(50, Math.max(0, next - this.#time())));
  }

  async #tick(): Promise<void> {
    if (this.#disposed || this.#status !== "playing") return;
    const next = this.#index + 1;
    if (next < this.recording.frames.length && this.recording.frames[next].timeMs <= this.#time())
      await this.#show(next);
    if (this.#disposed || this.#restarting) return;
    if (this.#status === "playing" && this.#index === this.recording.frames.length - 1 &&
        this.#time() >= this.recording.durationMs) {
      this.#position = this.recording.durationMs;
      this.#status = "ended";
    }
    this.#notify();
    this.#schedule();
  }

  async #show(index: number): Promise<void> {
    this.#busy = true;
    this.#pending = this.present(this.recording.frames[index]);
    try {
      await this.#pending;
      if (!this.#disposed) this.#index = index;
    } finally {
      this.#busy = false;
    }
  }
}
