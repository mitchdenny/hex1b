import type { WebTerminalRecordingHandle } from "@hex1b/web-terminal";
import { mountSourceBrowser } from "./source-browser";
import type { SampleFile } from "./sample-files";
import { cloneCommand, isSampleRepository, type SampleRepository } from "./clone-command";
import { element } from "./dom";
const clone = element("clone", HTMLButtonElement);
const clonePanel = element("clone-panel", HTMLDivElement);
const cloneText = element("clone-command", HTMLPreElement);
const copyClone = element("copy-clone", HTMLButtonElement);
const cloneStatus = element("clone-status", HTMLParagraphElement);
let player: WebTerminalRecordingHandle | undefined;
let sourceBrowser: ReturnType<typeof mountSourceBrowser> | undefined;

function reportError(error: unknown): void {
  const target = element("sample-error", HTMLParagraphElement);
  target.hidden = false;
  target.textContent = error instanceof Error ? error.message : String(error);
  target.dataset.level = "error";
  console.error(error);
}

type Sample = {
  title: string; description: string; command: string;
  files: SampleFile[]; repository: SampleRepository;
} & ({ sourceOnly: true; recording?: never } | { sourceOnly?: false; recording: string });

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function validateSample(value: unknown): asserts value is Sample {
  if (!isObject(value) || typeof value.title !== "string" || typeof value.description !== "string" ||
      typeof value.command !== "string" ||
      !(value.sourceOnly === true ? value.recording === undefined :
        (value.sourceOnly === undefined || value.sourceOnly === false) && typeof value.recording === "string" &&
        /^[a-zA-Z0-9._-]+$/u.test(value.recording)) ||
      !isSampleRepository(value.repository) ||
      !Array.isArray(value.files) || !value.files.length ||
      !value.files.every(file => isObject(file) && typeof file.path === "string" &&
        typeof file.language === "string" && typeof file.content === "string"))
    throw new Error("Invalid generated sample manifest");
}

async function initialize(): Promise<void> {
  const base = new URL(document.documentElement.dataset.siteBase ?? import.meta.env.BASE_URL, document.baseURI).href;
  const host = document.querySelector<HTMLElement>("[data-sample-manifest]");
  const manifestUrl = new URL(host?.dataset.sampleManifest ?? "recordings/sample.json", base);
  const response = await fetch(manifestUrl);
  if (!response.ok) throw new Error(`Could not load sample manifest: HTTP ${response.status}. Run npm run build first.`);
  const sample: unknown = await response.json();
  validateSample(sample);
  element("command", HTMLPreElement).textContent = sample.command;
  cloneText.textContent = cloneCommand(base, sample.repository);
  element("clone-run", HTMLPreElement).textContent = `cd ${sample.repository.directory}\n${sample.command}`;
  clone.disabled = false;
  sourceBrowser = mountSourceBrowser({
    root: element("source-browser", HTMLElement),
    tree: element("files", HTMLElement),
    editor: element("source-editor", HTMLElement),
    fallback: element("source-fallback", HTMLElement),
    path: element("source-path", HTMLElement),
    language: element("source-language", HTMLElement),
    status: element("source-status", HTMLElement),
  }, sample.files);
  if (sample.sourceOnly) return;
  element("description", HTMLParagraphElement).textContent = sample.description;
  const { mountPlayback } = await import("./recording-view");
  player = await mountPlayback(base, new URL(sample.recording, manifestUrl).href);
}

clone.addEventListener("click", () => {
  clonePanel.hidden = !clonePanel.hidden;
  clone.setAttribute("aria-expanded", String(!clonePanel.hidden));
});
copyClone.addEventListener("click", async () => {
  copyClone.disabled = true;
  try {
    if (!navigator.clipboard) throw new Error("Clipboard access requires HTTPS or localhost");
    await navigator.clipboard.writeText(cloneText.textContent ?? "");
    cloneStatus.textContent = "Clone command copied.";
    cloneStatus.dataset.level = "ready";
  } catch (error) {
    cloneStatus.textContent = `Could not copy: ${error instanceof Error ? error.message : String(error)}. Select the command and copy it manually.`;
    cloneStatus.dataset.level = "error";
  } finally {
    copyClone.disabled = false;
  }
});
window.addEventListener("pagehide", () => {
  player?.dispose();
  sourceBrowser?.dispose();
}, { once: true });
initialize().catch(reportError);
