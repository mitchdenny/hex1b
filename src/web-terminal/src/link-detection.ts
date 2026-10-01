import type { HyperlinkRange, TerminalCell } from "./wire-types.js";
import type { SelectionRange } from "./types.js";
import type { TerminalLinkAction, TerminalLinkActivation, TerminalLinkDetectionError,
  TerminalLinkOptions, TerminalLinkRule, TerminalLinkTextMode } from "./link-types.js";
import { LINK_LIMITS, normalizeLinks, validateLinkAction } from "./link-options.js";
import { extractLinkText, mapLinkRange } from "./link-text.js";
import type { MappedLinkChunk } from "./link-text.js";
import type { LinkScanMatch, LinkScanRequest, LinkScanResponse } from "./link-worker-protocol.js";
import { linkMatchTextSize } from "./link-worker-protocol.js";
import { defaultWorkerUrl } from "./worker-url.js";

export interface LinkDetectionSnapshot {
  revision: number; columns: number; rows: number;
  cells: readonly (TerminalCell | undefined)[];
  hyperlinks: readonly HyperlinkRange[];
}
export interface DetectedLink {
  id: string;
  action: TerminalLinkAction;
  activation: TerminalLinkActivation;
}
interface Work {
  identity: number; revision: number; snapshot: LinkDetectionSnapshot;
  chunks: Map<TerminalLinkTextMode, MappedLinkChunk[]>;
}
interface Job { id: number; work: Work; rule: TerminalLinkRule; keys: Map<string, string> }

function validResponse(response: LinkScanResponse, job: Job): boolean {
  if (!Array.isArray(response.results) || response.results.length !== job.keys.size) return false;
  const keys = new Set<string>();
  let count = 0, resultText = 0;
  for (const result of response.results) {
    if (!result || !job.keys.has(result.key) || keys.has(result.key) || !Array.isArray(result.matches)) return false;
    keys.add(result.key);
    count += result.matches.length;
    if (count > LINK_LIMITS.matches) return false;
    for (const match of result.matches) {
      if (!match || !Number.isSafeInteger(match.index) || match.index < 0 ||
          typeof match.text !== "string" || !match.text.length || !Array.isArray(match.captures) ||
          !match.captures.every(capture => capture === undefined || typeof capture === "string") ||
          !match.groups || typeof match.groups !== "object" || Array.isArray(match.groups) ||
          !Object.values(match.groups).every(group => group === undefined || typeof group === "string")) return false;
      resultText += linkMatchTextSize(match);
      if (resultText > LINK_LIMITS.resultText) return false;
    }
  }
  return true;
}

export class LinkDetection {
  #options: { workerUrl?: string | URL; actions: ReadonlySet<string>;
    onChange: (revision: number, links: readonly DetectedLink[]) => void;
    onError: (error: TerminalLinkDetectionError) => void };
  #detection: TerminalLinkOptions["detection"];
  #worker: Worker | null = null;
  #timer: ReturnType<typeof setTimeout> | undefined;
  #job: Job | null = null;
  #work: Work | null = null;
  #links: readonly DetectedLink[] = [];
  #disabled = new Set<string>();
  #cache = new Map<string, LinkScanMatch[]>();
  #cacheSizes = new Map<string, number>();
  #cacheSize = 0;
  #identity = 0;
  #jobId = 0;
  #revision = 0;
  #disposed = false;
  #workerFailed = false;
  #pumping = false;

  constructor(options: { workerUrl?: string | URL; actions: ReadonlySet<string>;
    onChange: (revision: number, links: readonly DetectedLink[]) => void;
    onError: (error: TerminalLinkDetectionError) => void }) {
    this.#options = { ...options, actions: new Set(options.actions) };
  }

  configure(detection: TerminalLinkOptions["detection"]): void {
    const normalized = normalizeLinks({ detection }, this.#options.actions);
    if (this.#disposed) return;
    this.#detection = normalized === false ? false : normalized.detection;
    this.#disabled.clear(); this.#workerFailed = false;
    this.clear();
  }

  update(snapshot: LinkDetectionSnapshot): void {
    if (this.#disposed || snapshot.revision < this.#revision) return;
    this.#revision = snapshot.revision;
    this.#work = null;
    this.#publish([]);
    if (!this.#detection || this.#workerFailed) return;
    const work: Work = { identity: ++this.#identity, revision: snapshot.revision,
      snapshot, chunks: new Map() };
    try {
      for (const rule of this.#detection.rules) {
        if (!rule.enabled || this.#disabled.has(rule.id)) continue;
        const mode = rule.text ?? "logicalLine";
        if (!work.chunks.has(mode)) work.chunks.set(mode, extractLinkText(snapshot, mode));
      }
    } catch (error) {
      this.#error("limit", null, error);
      return;
    }
    this.#work = work;
    this.#pump();
  }

  advance(revision: number): void {
    if (this.#disposed || revision < this.#revision) return;
    this.#revision = revision;
    if (this.#work) this.#work.revision = revision;
    this.#publish(this.#links.map(link => Object.freeze({ ...link,
      activation: Object.freeze({ ...link.activation, revision }) })));
  }

  clear(): void {
    this.#terminate();
    this.#identity++;
    this.#work = null;
    this.#cache.clear(); this.#cacheSizes.clear(); this.#cacheSize = 0;
    if (!this.#disposed) this.#publish([]);
  }

  dispose(): void {
    if (this.#disposed) return;
    this.clear();
    this.#disposed = true;
  }

  #publish(links: readonly DetectedLink[]): void {
    this.#links = Object.freeze([...links]);
    this.#options.onChange(this.#revision, this.#links);
  }

  #error(code: TerminalLinkDetectionError["code"], ruleId: string | null, error: unknown): void {
    this.#options.onError(Object.freeze({ code, ruleId, revision: this.#revision,
      message: error instanceof Error ? error.message : String(error) }));
  }

  #terminate(): void {
    clearTimeout(this.#timer); this.#timer = undefined;
    if (this.#worker) {
      this.#worker.terminate(); this.#worker = null;
    }
    this.#job = null;
  }

  #failWorker(error: unknown): void {
    const ruleId = this.#job?.rule.id ?? null;
    this.#terminate(); this.#workerFailed = true;
    this.#publish([]);
    this.#error("worker", ruleId, error);
  }

  #pump(): void {
    if (this.#pumping || this.#disposed || this.#job || !this.#work || !this.#detection || this.#workerFailed) return;
    this.#pumping = true;
    try {
      const work = this.#work;
      for (const rule of this.#detection.rules) {
        if (!rule.enabled || this.#disabled.has(rule.id)) continue;
        const chunks = work.chunks.get(rule.text ?? "logicalLine") ?? [];
        const missing = chunks.filter(chunk => !this.#cache.has(this.#cacheKey(rule, chunk)));
        if (!missing.length) continue;
        try {
          if (!this.#worker) {
            const worker = new Worker(this.#options.workerUrl ?? defaultWorkerUrl("link-detection"),
              { type: "module", name: "hex1b-link-detection" });
            this.#worker = worker;
            worker.addEventListener("message", event => {
              if (this.#worker === worker) this.#receive(event.data as LinkScanResponse);
            });
            worker.addEventListener("error", event => {
              if (this.#worker !== worker) return;
              event.preventDefault(); this.#failWorker(event.message);
            });
            worker.addEventListener("messageerror", () => {
              if (this.#worker === worker) this.#failWorker("Invalid detection worker message");
            });
          }
          const id = ++this.#jobId;
          this.#job = { id, work, rule, keys: new Map(missing.map(chunk => [chunk.key, this.#cacheKey(rule, chunk)])) };
          this.#timer = setTimeout(() => {
            if (this.#job?.id !== id) return;
            this.#terminate(); this.#disabled.add(rule.id);
            this.#error("timeout", rule.id, `Link rule exceeded ${LINK_LIMITS.timeoutMs} ms`);
            this.#pump();
          }, LINK_LIMITS.timeoutMs);
          const request: LinkScanRequest = { id,
            rule: rule.builtin ? { builtin: rule.builtin } : { source: rule.pattern!.source, flags: rule.pattern!.flags },
            chunks: missing.map(({ chunk, key }) => ({ key, text: chunk.text })) };
          this.#worker.postMessage(request);
        } catch (error) { this.#failWorker(error); }
        return;
      }
      this.#resolve(work);
    } finally { this.#pumping = false; }
  }

  #cacheKey(rule: TerminalLinkRule, chunk: MappedLinkChunk): string {
    return JSON.stringify([rule.id, chunk.key]);
  }

  #receive(response: LinkScanResponse): void {
    const job = this.#job;
    if (!job || response?.id !== job.id || this.#disposed) return;
    clearTimeout(this.#timer); this.#timer = undefined; this.#job = null;
    if (response.error) {
      this.#disabled.add(job.rule.id);
      this.#error(response.error, job.rule.id, response.message ?? "Link scan failed");
    } else if (!validResponse(response, job)) {
      this.#failWorker("Malformed detection response"); return;
    } else {
      for (const result of response.results!) {
        const key = job.keys.get(result.key)!;
        const size = key.length + result.matches.reduce((total, match) => total + linkMatchTextSize(match), 0);
        this.#cacheSize += size - (this.#cacheSizes.get(key) ?? 0);
        this.#cacheSizes.set(key, size);
        this.#cache.set(key, result.matches);
      }
    }
    if (this.#work !== job.work) this.#trimCache();
    this.#pump();
  }

  #resolve(work: Work): void {
    if (!this.#detection || this.#work !== work) return;
    const occupied: SelectionRange[] = [...work.snapshot.hyperlinks];
    const links: DetectedLink[] = [];
    const overlaps = (ranges: SelectionRange[]) => ranges.some(range => occupied.some(other =>
      range.row === other.row && range.startColumn < other.endColumn && range.endColumn > other.startColumn));
    for (const rule of this.#detection.rules) {
      if (!rule.enabled || this.#disabled.has(rule.id)) continue;
      const ruleLinks: DetectedLink[] = [], ruleRanges: SelectionRange[] = [];
      try {
        for (const mapped of work.chunks.get(rule.text ?? "logicalLine") ?? []) {
          for (const candidate of this.#cache.get(this.#cacheKey(rule, mapped)) ?? []) {
            const ranges = mapLinkRange(mapped, candidate.index, candidate.text.length);
            if (!ranges || overlaps(ranges)) continue;
            const match = Object.freeze({ ...candidate, captures: Object.freeze([...candidate.captures]),
              groups: Object.freeze({ ...candidate.groups }), chunk: mapped.chunk });
            const resolution = rule.resolve ? rule.resolve(match) : { target: candidate.text };
            if (this.#work !== work || this.#disposed) return;
            if (resolution === null) continue;
            if (typeof resolution !== "object" || resolution === undefined ||
                "then" in resolution || typeof resolution.target !== "string") throw new TypeError("Resolver must return a synchronous link resolution or null");
            const action = resolution.action === undefined ? rule.action : resolution.action;
            validateLinkAction(action, this.#options.actions);
            if (links.length + ruleLinks.length >= LINK_LIMITS.matches) throw new RangeError("Resolved link limit exceeded");
            const activation: TerminalLinkActivation = Object.freeze({
              source: "detected", ruleId: rule.id,
              kind: rule.builtin ? (rule.builtin === "url" || rule.builtin === "uri" ? "uri" : "path") : rule.kind!,
              text: candidate.text, target: resolution.target,
              ranges: Object.freeze(ranges.map(range => Object.freeze(range))),
              revision: work.revision, data: resolution.data,
            });
            ruleLinks.push(Object.freeze({ id: JSON.stringify([work.identity, rule.id,
              ranges[0].row, ranges[0].startColumn, candidate.index]),
              action, activation }));
            ruleRanges.push(...ranges);
          }
        }
        links.push(...ruleLinks); occupied.push(...ruleRanges);
      } catch (error) {
        this.#disabled.add(rule.id);
        this.#error(error instanceof RangeError ? "limit" : "resolver", rule.id, error);
        if (this.#work !== work || this.#disposed) return;
      }
    }
    // Evict only after resolution so a viewport larger than the cache cannot cause endless rescans.
    this.#trimCache();
    this.#publish(links);
  }

  #trimCache(): void {
    while (this.#cache.size > LINK_LIMITS.cacheEntries || this.#cacheSize > LINK_LIMITS.cacheText) {
      const key = this.#cache.keys().next().value!;
      this.#cache.delete(key); this.#cacheSize -= this.#cacheSizes.get(key)!;
      this.#cacheSizes.delete(key);
    }
  }
}
