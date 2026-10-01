import {
  WebTerminal, getCmdlineUrl, type WebTerminalOptions, type TerminalScrollbarTooltipRenderer
} from "@hex1b/web-terminal";
import { customScrollbarTooltip } from "./scrollbar-renderer.js";
import { followTerminalAppearance, forgetTerminalAppearance, terminalAppearance } from "./appearance.js";

/** A host-owned portal: its connection and DOM share the tooltip rendering's lifetime. */
export function createTerminalPreviewTooltip(options: {
  url: URL;
  terminal: () => WebTerminal | undefined;
  renderer: WebTerminalOptions["renderer"];
  font: WebTerminalOptions["font"];
}): TerminalScrollbarTooltipRenderer {
  return context => {
    if (context.marker.source !== "command") return customScrollbarTooltip(context);
    const source = options.terminal();
    if (!source?.connected || context.signal.aborted) return null;

    const card = document.createElement("div");
    card.className = "demo-terminal-preview";
    card.setAttribute("role", "tooltip");
    card.dataset.marker = context.marker.id;
    card.dataset.state = "loading";
    const heading = document.createElement("div");
    heading.className = "demo-scrollbar-tooltip-heading";
    const title = document.createElement("strong");
    title.textContent = "Command preview";
    const phase = document.createElement("span");
    phase.textContent = `${context.marker.phase ?? "unknown"}` +
      (context.marker.exitCode == null ? "" : ` / exit ${context.marker.exitCode}`);
    heading.append(title, phase);
    const command = document.createElement("div");
    command.className = "demo-terminal-preview-command";
    const encoded = getCmdlineUrl(context.details);
    if (encoded) {
      try { command.textContent = decodeURIComponent(encoded); }
      catch (error) {
        if (!(error instanceof URIError)) throw error;
        command.textContent = `Invalid command encoding: ${encoded}`;
      }
    } else {
      command.textContent = context.loading ? "Loading command details..."
        : context.error ? `Command details unavailable: ${context.error}`
        : "The shell did not provide command text.";
    }
    const status = document.createElement("div");
    status.className = "demo-terminal-preview-status";
    status.setAttribute("role", "status");
    status.textContent = "Loading retained output...";
    const mount = document.createElement("div");
    mount.className = "demo-terminal-preview-mount";
    mount.inert = true;
    const caption = document.createElement("div");
    caption.className = "demo-terminal-preview-caption";
    caption.textContent = "Read-only retained output, not a recording of the screen.";
    card.append(heading, command, status, mount, caption);
    document.body.append(card);

    let preview: WebTerminal | undefined;
    let failed = false;
    const fail = (error: unknown) => {
      if (context.signal.aborted || failed) return;
      failed = true;
      card.dataset.state = "error";
      status.setAttribute("role", "alert");
      status.textContent = `Preview unavailable: ${error instanceof Error ? error.message : String(error)}`;
      forgetTerminalAppearance(preview);
      preview?.dispose();
    };
    const position = () => {
      const width = Math.min(640, Math.max(0, window.innerWidth - 24));
      card.style.width = `${width}px`;
      const { geometry } = source;
      const aspect = geometry.rows * geometry.cellHeight / (geometry.columns * geometry.cellWidth);
      mount.style.height = `${Math.max(80, Math.min(280, (width - 24) * aspect))}px`;
      const origin = source.element.getBoundingClientRect();
      const scaleX = origin.width / context.layout.width;
      const scaleY = origin.height / context.layout.height;
      const anchorLeft = origin.left + context.anchor.left * scaleX;
      const anchorRight = anchorLeft + context.anchor.width * scaleX;
      const preferredLeft = anchorLeft - card.offsetWidth - 12;
      const left = preferredLeft >= 12 ? preferredLeft : anchorRight + 12;
      card.style.left = `${Math.max(12, Math.min(left, window.innerWidth - card.offsetWidth - 12))}px`;
      const center = origin.top + (context.anchor.top + context.anchor.height / 2) * scaleY;
      card.style.top = `${Math.max(12, Math.min(center - card.offsetHeight / 2,
        window.innerHeight - card.offsetHeight - 12))}px`;
    };
    const observer = new ResizeObserver(position);
    observer.observe(card);
    window.addEventListener("resize", position, { signal: context.signal });
    document.addEventListener("scroll", position, { capture: true, signal: context.signal });
    position();

    const open = async () => {
      if (context.signal.aborted) return;
      try {
        const url = new URL(options.url);
        url.searchParams.set("preview", "true");
        url.searchParams.set("name", "Command preview");
        url.searchParams.set("view", crypto.randomUUID());
        url.searchParams.delete("failure");
        preview = await WebTerminal.mount(mount, {
          ...terminalAppearance(),
          url, signal: context.signal, renderer: options.renderer, font: options.font,
          readOnly: true, scrollbar: false, links: false, padding: 0,
          label: "Read-only command output preview",
          onStatus(message, level) { if (level === "error") fail(message); },
          onClose(details) { fail(details.reason || `Connection closed (${details.code})`); }
        });
        if (context.signal.aborted || failed) {
          preview.dispose();
          return;
        }
        followTerminalAppearance(preview, context.signal);
        await preview.scrollToMarker(context.marker.id);
        if (context.signal.aborted || failed) return;
        card.dataset.state = "ready";
        status.textContent = `Retained output at row ${preview.viewport.top ?? context.marker.row}`;
      } catch (error) {
        fail(error);
      }
    };
    // Details replace the tooltip rendering once resolved; don't open a throwaway view.
    const timer = context.loading ? undefined : window.setTimeout(() => { void open(); }, 200);
    context.signal.addEventListener("abort", () => {
      clearTimeout(timer);
      observer.disconnect();
      preview?.dispose();
      card.remove();
    }, { once: true });
    return null;
  };
}
