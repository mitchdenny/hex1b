async page => {
  await page.setContent('<div id="terminal" style="width:800px;height:480px"></div>');
  return await page.evaluate(async () => {
    const name = new URL(location.href).searchParams.get("frame");
    if (!name || name.startsWith("/") || name.split("/").some(part => part === ".."))
      throw new Error("A reviewed relative frame path is required");
    const frameUrl = new URL(`/evidence/${name}`, location.href).href;
    const workerModule = new URL("/web-terminal/terminal-worker.js", location.href).href;
    const source = `
      const pending = [];
      const capture = event => pending.push(event.data);
      self.addEventListener("message", capture);
      const response = await fetch(${JSON.stringify(frameUrl)});
      if (!response.ok) throw new Error("Captured frame fetch failed");
      const frame = await response.arrayBuffer();
      self.WebSocket = class extends EventTarget {
        static OPEN = 1;
        readyState = 0;
        constructor() {
          super();
          queueMicrotask(() => {
            this.readyState = 1;
            this.dispatchEvent(new Event("open"));
            this.dispatchEvent(new MessageEvent("message", { data: frame }));
          });
        }
        send(message) {
          if (JSON.parse(message).type === "ack")
            self.postMessage({ type: "status", message: "Captured frame acknowledged", level: "info" });
        }
        close() { this.readyState = 3; }
      };
      await import(${JSON.stringify(workerModule)});
      self.removeEventListener("message", capture);
      for (const data of pending) self.dispatchEvent(new MessageEvent("message", { data }));
    `;
    const { WebTerminal } = await import("/web-terminal/index.js");
    const workerUrl = URL.createObjectURL(new Blob([source], { type: "text/javascript" }));
    const status = [];
    try {
      const terminal = await WebTerminal.mount(document.querySelector("#terminal"), {
        url: "/ws", workerUrl, renderer: "webgpu", onStatus: message => status.push(message)
      });
      // Retain the actual mounted client for screenshots; navigating away closes this fixture.
      window.graphicsCapture = { terminal, workerUrl };
      window.addEventListener("pagehide", () => {
        terminal.dispose();
        URL.revokeObjectURL(workerUrl);
      }, { once: true });
      return { provenance: "actual mounted client/worker/GPU; only WebSocket replaced with one captured HWT1 frame",
        status, stats: terminal.stats, placeholderTextPresent: terminal.screenText.includes("\u{10eeee}") };
    } catch (error) {
      URL.revokeObjectURL(workerUrl);
      throw error;
    }
  });
}
