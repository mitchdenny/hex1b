import type { HistoryMetadata } from "./wire-types.js";
import type { TerminalMarker } from "./scrollbar-types.js";
import { LIMITS } from "./protocol.js";

/** Retained inventories are a single presentation transaction, even when delivered over several frames. */
export class MarkerPages {
  #current: { revision: string; total: number; signature: string; markers: TerminalMarker[];
    ids: Set<string>; bytes: number } | undefined;
  get pending(): boolean { return this.#current !== undefined; }
  reset(): void { this.#current = undefined; }

  accept(history: HistoryMetadata | null): HistoryMetadata | null | undefined {
    if (!history?.markerPage) {
      this.reset();
      return history;
    }
    const { markers = [], markerPage: page, ...snapshot } = history;
    const signature = JSON.stringify(snapshot);
    if (page.offset === 0) this.#current = {
      revision: page.revision, total: page.total, signature, markers: [], ids: new Set(), bytes: 0
    };
    const current = this.#current;
    if (!current || current.revision !== page.revision || current.total !== page.total ||
        current.markers.length !== page.offset || current.signature !== signature)
      throw new Error("Marker pages do not describe the same contiguous history snapshot");
    current.bytes += new TextEncoder().encode(JSON.stringify(markers)).byteLength;
    if (current.bytes > LIMITS.metadataBytes) throw new Error("Retained marker inventory exceeds the 8 MiB client budget");
    for (const marker of markers) {
      if (current.ids.has(marker.id)) throw new Error("Duplicate marker ID across inventory pages");
      current.ids.add(marker.id);
      current.markers.push(marker);
    }
    if (current.markers.length < current.total) return undefined;
    this.reset();
    return { ...snapshot, markers: current.markers, markerPage: null };
  }
}
