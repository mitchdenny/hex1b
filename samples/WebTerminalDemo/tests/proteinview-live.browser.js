async page => {
  // Run on this demo's loopback /health URL with explicit executable/model query parameters.
  const configuration = await page.evaluate(() => {
    const query = new URL(location.href).searchParams;
    return { executable: query.get("executable"), model: query.get("model"),
      backend: query.get("backend") || "webgpu" };
  });
  if (!configuration.executable || !configuration.model)
    throw new Error("Explicit reviewed ProteinView executable and local model paths are required");
  await page.setContent('<div id="terminal" style="width:800px;height:480px"></div>');
  await page.evaluate(async configuration => {
    const response = await fetch("/api/terminals", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ scene: "shell", columns: 80, rows: 24, name: "ProteinView acceptance" })
    });
    if (!response.ok) throw new Error(`Terminal creation failed: ${response.status}`);
    const instance = await response.json();
    const { WebTerminal } = await import("/web-terminal/index.js");
    const state = window.proteinViewAcceptance = {
      instanceId: instance.id, changes: [], statuses: [], errors: [],
      peakTextureBytes: 0, peakImageCount: 0, peakAtlasBytes: 0, lastImageUploadBytes: 0
    };
    try {
      state.terminal = await WebTerminal.mount(document.querySelector("#terminal"), {
        url: `/ws?instance=${encodeURIComponent(instance.id)}`,
        renderer: configuration.backend,
        onStatus: (message, level) => {
          state.statuses.push({ message, level });
          if (level === "error") state.errors.push(message);
        },
        onStats: stats => {
          state.peakTextureBytes = Math.max(state.peakTextureBytes, stats.textureBytes);
          state.peakImageCount = Math.max(state.peakImageCount, stats.imageCount);
          state.peakAtlasBytes = Math.max(state.peakAtlasBytes, stats.atlasBytes);
          if (stats.imageCount && stats.imageUploadBytes > state.lastImageUploadBytes) {
            state.lastImageUploadBytes = stats.imageUploadBytes;
            if (state.changes.length < 256)
              state.changes.push({ frame: stats.frames, textureBytes: stats.textureBytes,
                imageCount: stats.imageCount, imageUploadBytes: stats.imageUploadBytes });
          }
        }
      });
      state.terminal.requestPrimary();
      state.terminal.focus();
      const quote = value => `'${value.replaceAll("'", "'\\''")}'`;
      state.command = `exec ${quote(configuration.executable)} ${quote(configuration.model)} --fullhd`;
    } catch (error) {
      await fetch(`/api/terminals/${instance.id}`, { method: "DELETE" });
      throw error;
    }
  }, configuration);
  try {
    await page.waitForFunction(() => window.proteinViewAcceptance.terminal.peer.isPrimary, null, { timeout: 10000 });
    await page.evaluate(() => {
      const state = window.proteinViewAcceptance;
      state.startedAt = Date.now();
      state.terminal.paste(state.command);
    });
    await page.keyboard.press("Enter");
    const runDeadline = Date.now() + 60000;
    const remaining = () => Math.max(1, runDeadline - Date.now());
    await page.waitForFunction(() => {
      const state = window.proteinViewAcceptance;
      if (state.errors.length) throw new Error(state.errors.join("; "));
      return state.terminal.stats.imageCount === 1 && state.terminal.screenText.includes("ProteinView");
    }, null, { timeout: Math.min(10000, remaining()) });
    await page.keyboard.press("Space");
    await page.waitForFunction(() => {
      const state = window.proteinViewAcceptance;
      if (state.errors.length) throw new Error(state.errors.join("; "));
      return state.changes.length >= 50;
    }, null, { timeout: remaining() });
    const beforeReset = await page.evaluate(() => window.proteinViewAcceptance.terminal.stats.imageUploadBytes);
    await page.keyboard.press("Space");
    await page.keyboard.press("r");
    await page.waitForFunction(before => window.proteinViewAcceptance.terminal.stats.imageUploadBytes > before,
      beforeReset, { timeout: Math.min(10000, remaining()) });

    // Read the actual compositor screenshot rather than a renderer-only test canvas.
    const screenshot = await page.screenshot();
    const result = await page.evaluate(async png => {
      const bitmap = await createImageBitmap(new Blob([new Uint8Array(png)], { type: "image/png" }));
      const canvas = document.createElement("canvas");
      canvas.width = bitmap.width;
      canvas.height = bitmap.height;
      const context = canvas.getContext("2d");
      context.drawImage(bitmap, 0, 0);
      bitmap.close();
      const data = context.getImageData(0, 0, canvas.width, canvas.height).data;
      const state = window.proteinViewAcceptance;
      const bounds = state.terminal.element.getBoundingClientRect();
      const scale = canvas.width / window.innerWidth;
      let coloredViewportPixels = 0;
      for (let y = Math.ceil((bounds.top + 60) * scale); y < (bounds.bottom - 60) * scale; y++) {
        for (let x = Math.ceil((bounds.left + 50) * scale); x < (bounds.right - 50) * scale; x++) {
          const offset = (y * canvas.width + x) * 4;
          const r = data[offset], g = data[offset + 1], b = data[offset + 2];
          if (Math.max(r, g, b) > 70 && Math.max(r, g, b) - Math.min(r, g, b) > 40)
            coloredViewportPixels++;
        }
      }
      if (coloredViewportPixels < 1000)
        throw new Error(`Insufficient visible molecular output: ${coloredViewportPixels} colored viewport pixels`);
      if (state.peakImageCount > 1)
        throw new Error(`Same-ID rotation retained ${state.peakImageCount} browser images`);
      if (state.peakTextureBytes > 80 * 24 * 10 * 20 * 4)
        throw new Error(`Texture ownership exceeded the full logical viewport: ${state.peakTextureBytes}`);
      return { instanceId: state.instanceId, startedAt: state.startedAt, completedAt: Date.now(), coloredViewportPixels,
        changingImagePresentationsObserved: state.changes.length,
        peakTextureBytes: state.peakTextureBytes, peakImageCount: state.peakImageCount,
        peakAtlasBytes: state.peakAtlasBytes, stats: state.terminal.stats,
        changes: state.changes, errors: state.errors,
        note: "Real sample socket/client/worker and unmodified app. Left paused for screenshots/reconnect checks; caller must end the owned terminal." };
    }, [...screenshot]);
    return result;
  } catch (error) {
    await page.evaluate(async () => {
      const state = window.proteinViewAcceptance;
      state.terminal?.dispose();
      const response = await fetch(`/api/terminals/${state.instanceId}`, { method: "DELETE" });
      if (!response.ok && response.status !== 404)
        console.error(`Cleanup failed: ${response.status}`);
    });
    throw error;
  }
}
