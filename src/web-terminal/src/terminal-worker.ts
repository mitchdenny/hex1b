import { decodeFrame, screenText } from "./protocol.js";
import { TerminalRenderer } from "./renderer.js";
import { LinkPresentation } from "./link-presentation.js";
import { MarkerPages } from "./marker-pages.js";
import type { TerminalSize, TerminalStatusLevel } from "./types.js";
import type { FrameMetadata, TerminalCell, TerminalCommand, WorkerInputMessage, WorkerOutputMessage, WorkerStats } from "./wire-types.js";
import { errorMessage } from "./validation.js";
import { compilePalette, defaultDarkPalette, normalizePalette } from "./terminal-palette.js";
import { createWebSocketTransport } from "./websocket-transport.js";
import { TransportSession } from "./transport-session.js";
import type { TerminalTransportConnection, TerminalTransportContext } from "./transport-types.js";

// This module is only executed as a dedicated worker. Keeping its global local to
// this module avoids leaking worker/WebGPU ambient dependencies to public declarations.
declare const self: {
  postMessage(message: WorkerOutputMessage): void;
  requestAnimationFrame(callback: FrameRequestCallback): number;
  close(): void;
  addEventListener(type: "error", callback: (event: ErrorEvent) => void): void;
  addEventListener(type: "unhandledrejection", callback: (event: PromiseRejectionEvent) => void): void;
  addEventListener(type: "message", callback: (event: MessageEvent<WorkerInputMessage>) => void): void;
};

let renderer: TerminalRenderer | undefined;
let transport: TransportSession | undefined;
let bridge: TerminalTransportContext | undefined;
let bridgeReady: ReturnType<typeof Promise.withResolvers<TerminalTransportConnection>> | undefined;
let bridgeSent: ReturnType<typeof Promise.withResolvers<void>> | undefined;
let failed = false;
let stopped = false;
let processing = false;
let drawing = false;
let frameInFlight = false;
let scheduled = false;
let needsRender = false;
let hasBlink = false;
let lastBlink = true;
let metadata: FrameMetadata | undefined;
let cells: (TerminalCell | undefined)[] = [];
let localRevision = 0;
let pendingFrame: { revision: number; full: boolean; changedCells: number } | undefined;
let renderPromise = Promise.resolve();
let metricsTimer: ReturnType<typeof setInterval> | undefined;
let blinkTimer: ReturnType<typeof setInterval> | undefined;
let viewport: TerminalSize | undefined;
const links = new LinkPresentation();
const markerPages = new MarkerPages();
let awaitingFull = false;
let requestedColorEncoding = false;
let palette = compilePalette(defaultDarkPalette);
const stats: WorkerStats = {
  revision: 0, fullFrames: 0, frames: 0, presentations: 0,
  changedCells: 0, lastChangedCells: 0, discardedFrames: 0,
  imageCount: 0, textureBytes: 0, atlasGlyphs: 0, atlasBytes: 0,
  bytesReceived: 0, imageUploadBytes: 0, imagePayloadBytes: 0,
  gpu: "initializing", connected: false, warnings: [],
  fps: 0, receivedKBps: 0, workloadMBps: 0,
  captureMs: 0, rendererCpuMs: 0, preparationCpuMs: 0,
  workloadBytes: 0, outputBatches: 0, serverElapsedMs: 0,
};
let sample = { time: performance.now(), presentations: 0, bytes: 0, workload: 0 };

function postStatus(message: string, level: TerminalStatusLevel = "info"): void {
  self.postMessage({ type: "status", message, level });
}

function send(message: TerminalCommand): void {
  if (stats.connected && !failed && !stopped) void transport?.send(JSON.stringify(message)).catch(fail);
}

function emitStats(text?: string): void {
  if (renderer && !renderer.disposed) Object.assign(stats, renderer.metrics());
  self.postMessage({ type: "stats", stats: { ...stats }, ...(text === undefined ? {} : { text }) });
}

function fail(error: unknown): void {
  if (failed || stopped) return;
  failed = true;
  const message = errorMessage(error);
  stats.gpu = "error";
  stats.connected = false;
  clearInterval(metricsTimer);
  clearInterval(blinkTimer);
  transport?.dispose();
  emitStats();
  postStatus(message, "error");
  renderer?.dispose();
}

self.addEventListener("error", event => {
  event.preventDefault();
  fail(event.error || new Error(event.message));
});
self.addEventListener("unhandledrejection", event => {
  event.preventDefault();
  fail(event.reason);
});

/** At most one state frame, one decode, and one GPU submission are outstanding. */
function scheduleRender() {
  needsRender = true;
  if (scheduled || drawing || processing || failed || stopped || !metadata || markerPages.pending || awaitingFull) return;
  scheduled = true;
  self.requestAnimationFrame(() => {
    scheduled = false;
    if (processing || drawing || failed || stopped || markerPages.pending || awaitingFull) return;
    renderPromise = drawFrame();
  });
}

async function drawFrame() {
  if (!needsRender || !metadata || !renderer) return;
  needsRender = false;
  drawing = true;
  const frame = pendingFrame;
  try {
    if (frame) links.prepare(cells, metadata);
    const linkSubmission = links.submission();
    renderer.resize(metadata.columns, metadata.rows, viewport);
    const blink = Math.floor(performance.now() / 600) % 2 === 0;
    const result = renderer.render(cells, metadata, blink, linkSubmission.mask, palette);
    // This is bounded completion/backpressure, not GPU readback or a GPU timing measurement.
    await renderer.idle();
    if (failed || stopped) return;
    lastBlink = blink;
    stats.presentations++;
    stats.rendererCpuMs = result.cpuMs;
    stats.quads = result.quads;
    stats.drawCalls = result.drawCalls;
    stats.warnings = renderer.canvasLimited
      ? [...metadata.warnings, `Canvas resolution capped by the GPU; terminal grid remains ${metadata.columns}x${metadata.rows}`]
      : metadata.warnings;
    if (frame) {
      pendingFrame = undefined;
      frameInFlight = false;
      stats.frames++;
      if (frame.full) stats.fullFrames++;
      stats.revision = frame.revision;
      stats.changedCells += frame.changedCells;
      stats.lastChangedCells = frame.changedCells;
      stats.captureMs = metadata.stats.captureMs;
      stats.workloadBytes = metadata.stats.workloadBytes;
      stats.outputBatches = metadata.stats.outputBatches;
      stats.serverElapsedMs = metadata.stats.elapsedMs;
      stats.columns = metadata.columns;
      stats.rows = metadata.rows;
      stats.mouseTracking = metadata.mouseTracking;
      stats.peer = metadata.peer;
      stats.history = metadata.history;
      const text = screenText(cells, metadata.columns, metadata.rows);
      self.postMessage({
        type: "geometry", columns: metadata.columns, rows: metadata.rows,
        cellWidth: metadata.cellWidth, cellHeight: metadata.cellHeight,
        mouseTracking: metadata.mouseTracking, peer: metadata.peer,
        history: metadata.history, revision: frame.revision, title: metadata.title,
        progress: metadata.progress, shellIntegration: metadata.shellIntegration,
        workingDirectory: metadata.workingDirectory, commandMark: metadata.commandMark,
        text, hyperlinks: metadata.hyperlinks, ...links.present(cells, metadata)
      });
      send({ type: "ack", revision: frame.revision });
      emitStats(text);
    } else {
      const snapshot = links.snapshot();
      if (snapshot) self.postMessage({ type: "linkSnapshot", generation: links.generation, snapshot });
    }
    const acknowledgement = links.acknowledge(linkSubmission.acknowledgement, processing || frameInFlight);
    if (acknowledgement) self.postMessage({ type: "linkDecorations", ...acknowledgement });
  } catch (error) {
    fail(error);
  } finally {
    drawing = false;
    if (needsRender && !processing) scheduleRender();
  }
}

async function receiveFrame(buffer: ArrayBuffer): Promise<void> {
  if (failed || stopped || !renderer) return;
  if (frameInFlight) throw new Error("Server sent a second state frame before acknowledgement");
  frameInFlight = true;
  processing = true;
  stats.bytesReceived += buffer.byteLength || 0;
  try {
    const frame = decodeFrame(buffer);
    const next = frame.metadata;
    if (!requestedColorEncoding && next.colorEncodings?.includes("indexed-v1")) {
      requestedColorEncoding = true;
      send({ type: "colorEncoding", value: "indexed-v1" });
    }
    if (!next.full && (awaitingFull || next.baseRevision !== localRevision || next.revision <= localRevision ||
        !metadata || next.columns !== metadata.columns || next.rows !== metadata.rows ||
        (next.colorEncoding ?? null) !== (metadata.colorEncoding ?? null))) {
      stats.discardedFrames++;
      frameInFlight = false;
      markerPages.reset();
      awaitingFull = true;
      needsRender = false;
      pendingFrame = undefined;
      // A discarded frame must release the server's one-in-flight gate before resync.
      send({ type: "ack", revision: next.revision });
      send({ type: "resync" });
      postStatus(`Revision mismatch (local ${localRevision}, base ${next.baseRevision}); requesting a full frame`);
      return;
    }
    await renderPromise;
    if (failed || stopped) return;
    // No blink presentation may reference textures while this resource transaction is in progress.
    await renderer.idle();
    await renderer.updateImages(frame.images, next.retainedImages);
    if (failed || stopped) return;
    const preparationStart = performance.now();
    const nextCells: (TerminalCell | undefined)[] = next.full
      ? new Array<TerminalCell | undefined>(next.columns * next.rows) : cells.slice();
    for (const cell of frame.cells) nextCells[cell.index] = cell;
    renderer.prepareGlyphs(nextCells);
    cells = nextCells;
    metadata = next;
    localRevision = next.revision;
    const wasPaging = markerPages.pending && !next.full;
    const history = markerPages.accept(next.history);
    awaitingFull = false;
    pendingFrame = { revision: next.revision,
      full: next.full || (wasPaging && !!pendingFrame?.full),
      changedCells: frame.cells.length + (wasPaging ? pendingFrame?.changedCells ?? 0 : 0) };
    hasBlink = cells.some(cell => cell && (cell.attributes & 16) && !(cell.attributes & 64)) ||
      (next.cursor.visible && (next.cursor.shape === 0 || next.cursor.shape % 2 === 1));
    stats.preparationCpuMs = performance.now() - preparationStart;
    if (history === undefined) {
      // Acknowledge an inventory fragment, but expose/draw only the completed snapshot.
      needsRender = false;
      frameInFlight = false;
      send({ type: "ack", revision: next.revision });
      return;
    }
    next.history = history;
    needsRender = true;
  } finally {
    processing = false;
    if (needsRender) scheduleRender();
  }
}

async function initialize(message: Extract<WorkerInputMessage, { type: "init" }>): Promise<void> {
  if (renderer || transport) throw new Error("Worker is already initialized");
  palette = compilePalette(normalizePalette(message.palette === undefined ? defaultDarkPalette : message.palette));
  if (typeof self.requestAnimationFrame !== "function") {
    throw new Error("This browser does not support requestAnimationFrame in a dedicated OffscreenCanvas worker");
  }
  postStatus("Loading terminal font and initializing renderer...");
  renderer = await TerminalRenderer.create(message.canvas, message.scale, fail, message.font, message.renderer);
  if (failed || stopped) {
    renderer?.dispose();
    return;
  }
  stats.gpu = "ready";
  stats.backingScale = message.scale;
  emitStats();
  const rendererName = renderer.backend.kind === "webgpu" ? "WebGPU" : "WebGL2";
  if (renderer.fallbackReason) postStatus(`Using WebGL2: ${renderer.fallbackReason}`);
  postStatus(`${rendererName} ready. Attaching terminal view...`);
  transport = new TransportSession({
    onReady() {
      if (failed || stopped) return;
      stats.connected = true;
      self.postMessage({ type: "connected" });
      postStatus(`Connected · ${rendererName} worker · server-authoritative cells and graphics`, "ready");
      emitStats();
    },
    onFrame: receiveFrame,
    onError: fail,
    onClose(details) {
      if (failed || stopped) return;
      stats.connected = false;
      stats.gpu = "stopped";
      stats.fps = 0;
      stopped = true;
      clearInterval(metricsTimer);
      clearInterval(blinkTimer);
      renderer?.dispose();
      self.postMessage({ type: "closed", details });
      postStatus(`View disconnected (${details.code === undefined ? details.reason :
        `${details.code}${details.reason ? `: ${details.reason}` : ""}`}). Attach another view to reconnect.`, "error");
      emitStats();
    }
  });
  metricsTimer = setInterval(() => {
    const now = performance.now();
    const seconds = (now - sample.time) / 1000;
    stats.fps = (stats.presentations - sample.presentations) / seconds;
    stats.receivedKBps = (stats.bytesReceived - sample.bytes) / seconds / 1000;
    stats.workloadMBps = Math.max(0, stats.workloadBytes - sample.workload) / seconds / 1000000;
    sample = { time: now, presentations: stats.presentations, bytes: stats.bytesReceived, workload: stats.workloadBytes };
    emitStats();
  }, 1000);
  blinkTimer = setInterval(() => {
    const blinkOn = Math.floor(performance.now() / 600) % 2 === 0;
    if (hasBlink && blinkOn !== lastBlink) scheduleRender();
  }, 100);
  transport.start(message.transport.type === "websocket" ? createWebSocketTransport(message.transport.url) : {
    connect(context) {
      bridge = context;
      bridgeReady = Promise.withResolvers<TerminalTransportConnection>();
      context.signal.addEventListener("abort", () => {
        bridgeReady?.reject(context.signal.reason);
        bridgeSent?.reject(context.signal.reason);
      }, { once: true });
      self.postMessage({ type: "transportConnect" });
      return bridgeReady.promise;
    }
  });
}

self.addEventListener("message", event => {
  const message = event.data;
  if (message.type === "init") {
    initialize(message).catch(fail);
  } else if (message.type === "transportConnected" && bridge && !failed && !stopped) {
    bridgeReady?.resolve({
      send(control) {
        bridgeSent = Promise.withResolvers<void>();
        self.postMessage({ type: "transportSend", control });
        return bridgeSent.promise;
      },
      dispose() {}
    });
  } else if (message.type === "transportFrame" && bridge && !failed && !stopped) {
    void bridge.onFrame(message.buffer).then(() => {
      if (!failed && !stopped) self.postMessage({ type: "transportReceived" });
    }).catch(fail);
  } else if (message.type === "transportSent" && !failed && !stopped) {
    bridgeSent?.resolve();
    bridgeSent = undefined;
  } else if (message.type === "transportClosed") {
    bridge?.onClose(message.details);
  } else if (message.type === "transportError") {
    bridge?.onError(new Error(message.message));
  } else if (message.type === "stop") {
    stopped = true;
    clearInterval(metricsTimer);
    clearInterval(blinkTimer);
    transport?.dispose();
    renderer?.dispose();
    self.close();
  } else if (message.type === "viewport" && !failed && !stopped) {
    if (!Number.isFinite(message.width) || !Number.isFinite(message.height) || message.width < 0 || message.height < 0) {
      fail(new Error("Invalid mounted viewport dimensions"));
      return;
    }
    if (viewport?.width === message.width && viewport?.height === message.height) return;
    viewport = { width: message.width, height: message.height };
    scheduleRender();
  } else if (message.type === "command" && !failed && !stopped) {
    send(message.command);
  } else if (message.type === "palette" && !failed && !stopped) {
    try {
      palette = compilePalette(normalizePalette(message.palette));
      scheduleRender();
    } catch (error) { fail(error); }
  } else if (message.type === "linkDetection" && !failed && !stopped) {
    try {
      if (!links.configure(message.enabled, message.generation)) return;
      if (!processing && !drawing && !pendingFrame) {
        const snapshot = links.snapshot();
        if (snapshot) self.postMessage({ type: "linkSnapshot", generation: links.generation, snapshot });
      }
      scheduleRender();
    } catch (error) { fail(error); }
  } else if (message.type === "linkDecorations" && !failed && !stopped) {
    try {
      if (links.accept(message.revision, message.generation, message.serial, message.ranges,
        processing || frameInFlight || !!pendingFrame, message.underlineStyle)) scheduleRender();
    } catch (error) { fail(error); }
  }
});
