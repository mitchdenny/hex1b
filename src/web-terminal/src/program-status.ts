import type { TerminalProgramStatusInventory,
  TerminalProgramStatusSnapshot } from "./types.js";
import { isRecord } from "./validation.js";

const encoder = new TextEncoder();
const idPattern = /^[A-Za-z0-9_.+-]{1,32}(?:\/[A-Za-z0-9_.+-]{1,32}){0,7}$/u;
const appPattern = /^[A-Za-z0-9_.+-]{1,32}$/u;

function text(value: unknown, limit: number): void {
  if (value === null) return;
  if (typeof value !== "string" || value.length > limit ||
      /[\u0000-\u001f\u007f-\u009f\ud800-\udfff]/u.test(value) ||
      encoder.encode(value).byteLength > limit) throw new Error("Invalid program status text");
}

/** Validates authoritative wire state independently of browser/GPU initialization. */
export function validateProgramStatus(value: unknown): asserts value is TerminalProgramStatusInventory {
  if (!isRecord(value) || !Array.isArray(value.records) || value.records.length > 256)
    throw new Error("Invalid program status inventory");
  const records = new Map<string, { id: string; app: unknown; effectiveApp: unknown }>();
  for (const record of value.records) {
    if (!isRecord(record) || typeof record.id !== "string" ||
        (record.id !== "" && (!idPattern.test(record.id) || record.id.length > 128)) ||
        records.has(record.id)) throw new Error("Invalid or duplicate program status ID");
    if (typeof record.state !== "string" || !["idle", "working", "blocked", "done", "error"].includes(record.state))
      throw new Error("Invalid program status state");
    if (record.kind !== null && (record.state !== "blocked" ||
        typeof record.kind !== "string" || !["permission", "question", "auth"].includes(record.kind)))
      throw new Error("Invalid program status kind");
    if (record.progress !== null && (!(record.state === "working" || record.state === "blocked") ||
        typeof record.progress !== "number" || !Number.isInteger(record.progress) ||
        record.progress < 0 || record.progress > 100))
      throw new Error("Invalid program status progress");
    for (const app of [record.app, record.effectiveApp])
      if (app !== null && (typeof app !== "string" || !appPattern.test(app)))
        throw new Error("Invalid program status application");
    text(record.title, 192);
    text(record.message, 2048);
    records.set(record.id, { id: record.id, app: record.app, effectiveApp: record.effectiveApp });
  }
  for (const record of records.values()) {
    let expected = record.app;
    let id = record.id;
    while (expected === null && id !== "") {
      const slash = id.lastIndexOf("/");
      id = slash < 0 ? "" : id.slice(0, slash);
      expected = records.get(id)?.app ?? null;
    }
    if (record.effectiveApp !== expected) throw new Error("Inconsistent program status application inheritance");
  }
}

export function programStatusSnapshot(inventory: TerminalProgramStatusInventory): TerminalProgramStatusSnapshot {
  const records = Object.freeze(inventory.records.map(record => Object.freeze({ ...record })));
  const index = new Map(records.map(record => [record.id, record]));
  return Object.freeze({
    records,
    find: (id: string) => index.get(id),
    getDescendants: (id: string) => Object.freeze(records.filter(record =>
      record.id !== id && (id === "" || record.id.startsWith(`${id}/`)))),
  });
}

export function sameProgramStatus(left: TerminalProgramStatusInventory, right: TerminalProgramStatusInventory): boolean {
  return left.records.length === right.records.length && left.records.every((record, index) => {
    const other = right.records[index]!;
    return record.id === other.id && record.state === other.state && record.kind === other.kind &&
      record.progress === other.progress && record.app === other.app &&
      record.effectiveApp === other.effectiveApp && record.title === other.title && record.message === other.message;
  });
}
