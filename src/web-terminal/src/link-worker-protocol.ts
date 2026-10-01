import type { TerminalLinkRule } from "./link-types.js";

export interface LinkScanRequest {
  id: number;
  rule: { source: string; flags: string } | { builtin: NonNullable<TerminalLinkRule["builtin"]> };
  chunks: { key: string; text: string }[];
}
export interface LinkScanMatch {
  index: number; text: string; captures: (string | undefined)[];
  groups: Record<string, string | undefined>;
}
export function linkMatchTextSize(match: LinkScanMatch): number {
  // Include entry overhead so empty capture groups cannot evade the payload budget.
  let size = match.text.length + 16;
  for (const capture of match.captures) size += 8 + (capture?.length ?? 0);
  for (const [name, group] of Object.entries(match.groups)) size += 8 + name.length + (group?.length ?? 0);
  return size;
}
export interface LinkScanResponse {
  id: number;
  results?: { key: string; matches: LinkScanMatch[] }[];
  error?: "limit" | "worker";
  message?: string;
}
