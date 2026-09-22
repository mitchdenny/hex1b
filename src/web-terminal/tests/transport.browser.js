async page => {
  // Serve src/web-terminal as the HTTP root; this fixture uses only the public bundle.
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the package's static HTTP server before running this fixture");
  const context = await page.context().browser().newContext();
  const test = await context.newPage();
  const errors = [];
  const sockets = [];
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => sockets.push(socket.url()));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  try {
    await test.goto(`${origin}/tests/`);
    const backends = await test.evaluate(async () => {
      return await navigator.gpu?.requestAdapter() ? ["webgl2", "webgpu"] : ["webgl2"];
    });
    const reports = [];
    for (const renderer of backends) {
      await test.setContent('<div id="terminal" style="width:400px;height:240px"></div>');
      await test.evaluate(async renderer => {
        const { WebTerminal } = await import("/dist/index.js");
        const check = (condition, message) => { if (!condition) throw new Error(message); };
        const attached = Promise.withResolvers();
        const acknowledgements = new Map();
        let context;
        let presentedRevision = 0;
        let disposed = 0;
        const controls = [];
        const notices = [];
        function frame(revision, { release = false, swapped = false } = {}) {
          const full = revision === 1;
          const images = full ? ["red", "green"].map(key => ({
            key, width: 1, height: 1, byteLength: 4, format: "rgba"
          })) : [];
          const metadata = {
            version: 1, full, revision, baseRevision: full ? 0 : revision - 1,
            columns: 4, rows: 2, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
            title: `Transport ${revision}`, progress: { state: "none", percentage: null },
            shellIntegration: { phase: "unknown", lastExitCode: null },
            workingDirectory: { uri: null, host: null, path: null }, commandMark: null,
            peer: { id: "custom", primaryId: "custom", isPrimary: true }, history: null,
            cursor: { x: 0, y: 0, visible: false, shape: 2 },
            images, retainedImages: release ? [] : ["red", "green"],
            placements: release ? [] : ["red", "green"].map((key, i) => ({
              key, kind: i ? "sixel" : "kgp", x: (swapped ? 1 - i : i) * 20, y: 0,
              width: 20, height: 40, sourceX: 0, sourceY: 0, sourceWidth: 1, sourceHeight: 1,
              clipX: 0, clipY: 0, clipWidth: 40, clipHeight: 40, z: 1
            })),
            warnings: [], hyperlinks: [],
            stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }
          };
          const json = new TextEncoder().encode(JSON.stringify(metadata));
          const bytes = new Uint8Array(12 + json.length + (full ? 8 * 23 + 8 : 0));
          const view = new DataView(bytes.buffer);
          view.setUint32(0, 0x31545748, true);
          view.setUint32(4, json.length, true);
          bytes.set(json, 8);
          view.setUint32(8 + json.length, full ? 8 : 0, true);
          let offset = 12 + json.length;
          if (full) {
            for (let i = 0; i < 8; i++, offset += 23) {
              view.setUint32(offset, i, true);
              view.setUint8(offset + 18, 1);
              view.setUint16(offset + 20, 1, true);
              view.setUint8(offset + 22, 32);
            }
            bytes.set([255, 0, 0, 255, 0, 255, 0, 255], offset);
          }
          return bytes.buffer;
        }
        const mount = WebTerminal.mount(document.getElementById("terminal"), {
          renderer, transport: {
            connect(value) {
              context = value;
              attached.resolve();
              return {
                async send(control) {
                  // This fixture is also the producer: it interprets ACKs, unlike an adapter.
                  const command = JSON.parse(control);
                  controls.push(command);
                  if (command.type === "ack") {
                    check(presentedRevision === command.revision, "ACK preceded completed presentation");
                    acknowledgements.get(command.revision)?.resolve();
                  }
                },
                dispose() { disposed++; }
              };
            }
          },
          onStats: stats => { presentedRevision = stats.revision; },
          onClose: details => notices.push(details),
        });
        await attached.promise;
        const firstAck = Promise.withResolvers();
        acknowledgements.set(1, firstAck);
        const buffer = frame(1);
        await context.onFrame(buffer);
        check(buffer.byteLength === 0, "Custom frame was not transferred to the worker");
        const terminal = await mount;
        await firstAck.promise;
        window.transportFixture = {
          terminal, controls, notices, context, get disposed() { return disposed; },
          async next(revision, options) {
            const ack = Promise.withResolvers();
            acknowledgements.set(revision, ack);
            await context.onFrame(frame(revision, options));
            await ack.promise;
          }
        };
      }, renderer);

      const pixels = async () => {
        const image = await test.locator("#terminal canvas:not(.scrollbar-canvas)").screenshot();
        return test.evaluate(async base64 => {
          const blob = new Blob([Uint8Array.from(atob(base64), c => c.charCodeAt(0))], { type: "image/png" });
          const bitmap = await createImageBitmap(blob);
          const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
          const ctx = canvas.getContext("2d");
          ctx.drawImage(bitmap, 0, 0);
          bitmap.close();
          const left = [...ctx.getImageData(Math.floor(canvas.width / 4), Math.floor(canvas.height / 2), 1, 1).data];
          const right = [...ctx.getImageData(Math.floor(canvas.width * 3 / 4), Math.floor(canvas.height / 2), 1, 1).data];
          return { left, right };
        }, image.toString("base64"));
      };
      const first = await pixels();
      check(first.left[0] > 240 && first.left[1] < 10 && first.right[1] > 240 && first.right[0] < 10,
        `KGP/Sixel pixels not presented: ${JSON.stringify(first)}`);
      await test.evaluate(() => transportFixture.next(2, { swapped: true }));
      const moved = await pixels();
      check(moved.left[1] > 240 && moved.right[0] > 240, `Retained images did not move: ${JSON.stringify(moved)}`);
      const stats = await test.evaluate(() => transportFixture.terminal.stats);
      check(stats.imageCount === 2 && stats.imageUploadBytes === 8 && stats.textureBytes === 8,
        `Retained resource lifetime changed: ${JSON.stringify(stats)}`);
      await test.evaluate(() => transportFixture.next(3, { release: true }));
      check(await test.evaluate(() => transportFixture.terminal.stats.imageCount === 0 &&
        transportFixture.terminal.stats.textureBytes === 0), "Images were not released");
      await test.evaluate(() => {
        transportFixture.terminal.paste("opaque input");
        transportFixture.terminal.resync();
      });
      await test.waitForFunction(() => transportFixture.controls.some(command => command.type === "resync"));
      check(await test.evaluate(() => transportFixture.controls.some(command =>
        command.type === "paste" && command.text === "opaque input")), "Input was not forwarded");
      await test.evaluate(() => transportFixture.context.onClose({ reason: "host detached" }));
      await test.waitForFunction(() => !transportFixture.terminal.connected);
      check(await test.evaluate(() => transportFixture.notices.length === 1 &&
        transportFixture.notices[0].reason === "host detached" &&
        transportFixture.notices[0].code === undefined), "Custom closure acquired WebSocket status");
      await test.evaluate(() => transportFixture.terminal.dispose());
      check(await test.evaluate(() => transportFixture.disposed === 1 &&
        document.getElementById("terminal").childElementCount === 0), "Dispose leaked the connection or DOM");
      reports.push({ renderer, first, moved, imageUploadBytes: stats.imageUploadBytes });
    }
    check(errors.length === 0, errors.join("; "));
    check(sockets.length === 0, "Custom transport opened a WebSocket");
    return { passed: true, reports, websockets: sockets.length };
  } finally { await context.close(); }
}
