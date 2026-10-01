import type { TerminalPlaybackState, WebTerminalRecordingHandle } from "@hex1b/web-terminal";
import { element } from "./dom";

export async function mountPlayback(base: string, recordingUrl: string): Promise<WebTerminalRecordingHandle> {
  const play = element("play", HTMLButtonElement);
  const pause = element("pause", HTMLButtonElement);
  const restart = element("restart", HTMLButtonElement);
  const status = element("status", HTMLParagraphElement);
  const progress = element("progress", HTMLProgressElement);
  const time = element("time", HTMLOutputElement);
  const container = element("terminal", HTMLDivElement);
  let player: WebTerminalRecordingHandle | undefined;
  let failed = false;
  function reportError(error: unknown): void {
    failed = true;
    status.textContent = error instanceof Error ? error.message : String(error);
    status.dataset.level = "error";
    play.disabled = pause.disabled = restart.disabled = true;
    console.error(error);
  }
  function showPlayback(state: TerminalPlaybackState): void {
    if (failed) return;
    play.disabled = state.status !== "paused";
    pause.disabled = state.status !== "playing";
    restart.disabled = false;
    progress.max = state.durationMs || 1;
    progress.value = state.positionMs;
    time.textContent = `${(state.positionMs / 1000).toFixed(1)} / ${(state.durationMs / 1000).toFixed(1)} s`;
    status.textContent = state.status === "ended"
      ? "Playback complete. The final frame is retained; restart to watch again."
      : state.status === "playing" ? "Playing captured terminal state locally." : "Paused. No terminal server is connected.";
    status.dataset.level = "ready";
    container.dataset.frame = String(state.frameIndex);
    container.dataset.playback = state.status;
  }
  play.addEventListener("click", () => { try { player?.play(); } catch (error) { reportError(error); } });
  pause.addEventListener("click", () => { try { player?.pause(); } catch (error) { reportError(error); } });
  restart.addEventListener("click", () => {
    try {
      play.disabled = pause.disabled = restart.disabled = true;
      player?.restart();
    } catch (error) { reportError(error); }
  });
  const download = element("download", HTMLAnchorElement);
  download.href = recordingUrl;
  download.hidden = false;
  try {
    const { WebTerminal }: typeof import("@hex1b/web-terminal") =
      await import(/* @vite-ignore */ `${base}web-terminal/index.js`);
    player = await WebTerminal.mountRecording(container, {
      url: recordingUrl,
      onPlaybackChange: showPlayback,
      onStatus(message, level) {
        if (level === "error") reportError(new Error(message));
        else if (!player) status.textContent = message;
      },
      onStats(stats, text) {
        element("metrics", HTMLParagraphElement).textContent =
          `${stats.renderer ?? "Initializing"} · ${stats.columns ?? 0} × ${stats.rows ?? 0} cells · ` +
          `${stats.imageCount ?? 0} image resources · ${stats.frames ?? 0} frames presented`;
        if (text !== undefined) element("screen-text", HTMLPreElement).textContent = text;
      },
    });
    showPlayback(player.playback);
    return player;
  } catch (error) {
    reportError(error);
    throw error;
  }
}
