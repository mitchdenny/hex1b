async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)[0];
  const results = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  for (const renderer of ["webgl2", "webgpu"]) {
    for (const transport of ["direct", "hmp1"]) {
      for (const scenario of ["features", "kgp", "sixel", "animation", "history", "discard-kgp"]) {
        for (const closure of scenario === "features" ? ["exit", "close", "abort"] : ["close", "abort"]) {
          const context = await page.context().browser().newContext({ viewport: { width: 1100, height: 800 } });
          const test = await context.newPage();
          const frames = [], controls = [], errors = [];
          let instance, stage = "mount";
          const label = `${renderer}/${transport}/${scenario}/${closure}`;
          test.on("pageerror", error => errors.push(error.message));
          test.on("websocket", socket => {
            socket.on("framesent", ({ payload }) => { if (typeof payload === "string") controls.push(JSON.parse(payload)); });
            socket.on("framereceived", ({ payload }) => {
              if (typeof payload !== "string" && payload.length >= 8 && payload.readUInt32LE(0) === 0x31545748)
                frames.push(JSON.parse(payload.subarray(8, 8 + payload.readUInt32LE(4)).toString("utf8")));
            });
          });
          try {
            await test.goto(`${origin}/health`);
            const response = await test.request.post(`${origin}/api/terminals`, {
              headers: { Origin: origin }, data: {
                scene: ["features", "history"].includes(scenario) ? "shell" : scenario === "discard-kgp" ? "kgp" : scenario,
                columns: 80, rows: 24, reflowStrategy: "none"
              }
            });
            check(response.status() === 201, `${label}: creation failed`);
            instance = (await response.json()).id;
            const viewId = await test.evaluate(async ({ instance, renderer, transport, scenario }) => {
              const { WebTerminal } = await import("/web-terminal/index.js");
              document.body.innerHTML = '<div id="retained" style="width:800px;height:480px"></div>';
              window.decorations = [];
              window.linkActivations = 0;
              const RealWorker = window.Worker;
              window.Worker = class extends RealWorker {
                postMessage(message, ...args) {
                  if (message.type === "linkDecorations") decorations.push(message);
                  super.postMessage(message, ...args);
                }
              };
              const viewId = crypto.randomUUID();
              window.retained = await WebTerminal.mount(document.getElementById("retained"), {
                url: `/ws?instance=${instance}&transport=${transport}&view=${viewId}`,
                renderer, preserveOnDisconnect: scenario !== "discard-kgp", colorMode: "dark",
                sizing: { mode: "fixed", columns: 80, rows: 24 },
                links: { osc8: { action: () => { linkActivations++; } }, detection: {
                  rules: [{ id: "url", builtin: "url", action: () => { linkActivations++; } }], decoration: "always"
                } },
                onClose: details => { window.retainedClose = details; }
              });
              return viewId;
            }, { instance, renderer, transport, scenario });
            if (["features", "history"].includes(scenario)) {
              stage = "shell prompt";
              await test.waitForFunction(() => /([#$%>]|[^\x00-\x7f])$/.test(retained.screenText.trimEnd()));
              const command = await test.evaluate(async ({ scenario, closure }) => {
                const esc = "\x1b";
                const kgp = (parameters, bytes) => `${esc}_G${parameters};${bytes ? btoa(String.fromCharCode(...bytes)) : ""}${esc}\\`;
                let output;
                if (scenario === "history") {
                  output = Array.from({ length: 60 }, (_, i) => `HISTORY-${String(i).padStart(2, "0")}\r\n`).join("") +
                    "HISTORY_READY\r\n";
                } else {
                  const png = document.createElement("canvas");
                  png.width = png.height = 2;
                  const paint = png.getContext("2d");
                  paint.fillStyle = "#00ff00"; paint.fillRect(0, 0, 2, 2);
                  const pngBytes = Uint8Array.from(atob(png.toDataURL().split(",")[1]), c => c.charCodeAt(0));
                  const compressed = new Uint8Array(await new Response(new Blob([
                    new Uint8Array([255, 0, 255, 255])
                  ]).stream().pipeThrough(new CompressionStream("deflate"))).arrayBuffer());
                  output = `${esc}[?1049h${esc}[?2026h${esc}[?25l${esc}[2J${esc}[H` +
                    `${esc}]2;EXOTIC_FINAL_TITLE\x07${esc}]9;4;2;37\x07` +
                    `${esc}]7;file:///tmp/preserved\x07${esc}]133;C;cmdline_url=exotic-check\x07` +
                    "EXOTIC_FINAL_READY e\u0301\u754c \ud83d\ude80\r\n" +
                    `${esc}[1;3;9;53mStyled${esc}[0m ${esc}[4:1mSolid${esc}[4:2mDouble` +
                    `${esc}[4:3mCurly${esc}[4:4mDotted${esc}[4:5mDashed${esc}[0m\r\n` +
                    `${esc}[5mBlink-visible${esc}[0m ${esc}[8mConcealed${esc}[0m\r\n` +
                    `${esc}[41m ANSI ${esc}[48;2;18;58;188m TRUECOLOR ${esc}[0m\r\n` +
                    `${esc}]8;;https://example.com/authored${esc}\\AUTHORED${esc}]8;;${esc}\\ https://example.com/detected\r\n` +
                    `${esc}#6DOUBLE-WIDTH e\u0301\u754c\r\n${esc}#3DOUBLE-HEIGHT\r\n${esc}#4DOUBLE-HEIGHT\r\n${esc}#5` +
                    `${esc}[10;1H` + kgp("a=T,f=32,s=1,v=1,i=1,p=1,c=6,r=3,C=1,z=1,q=2", new Uint8Array([255, 0, 0, 255])) +
                    `${esc}[10;10H` + kgp("a=T,f=100,i=2,p=2,c=6,r=3,C=1,z=1,q=2", pngBytes) +
                    `${esc}[10;20H` + kgp("a=T,f=32,s=1,v=1,i=3,p=3,c=6,r=3,C=1,z=1,o=z,q=2", compressed) +
                    `${esc}[10;40H` + kgp("a=T,f=24,s=2,v=1,i=6,p=6,c=3,r=3,w=1,h=1,C=1,z=1,q=2",
                      new Uint8Array([128, 0, 255, 0, 0, 0])) +
                    `${esc}[15;1H${esc}P7;1q"1;1;20;12#1;2;0;100;100#1!20~-!20~${esc}\\` +
                    kgp("a=t,f=32,s=1,v=1,i=4,q=2", new Uint8Array([255, 128, 0, 255])) +
                    kgp("a=p,i=4,p=4,c=3,r=2,P=1,Q=1,H=30,V=0,C=1,z=1,q=2") +
                    kgp("a=T,f=32,s=1,v=1,i=5,p=5,c=2,r=1,U=1,C=1,q=2", new Uint8Array([0, 0, 255, 255])) +
                    `${esc}[15;10H${esc}[38;2;0;0;5m\u{10eeee}\u0305\u0305\u{10eeee}\u0305\u030d${esc}[0m` +
                    `${esc}[18;1H${esc}[2mDim${esc}[0m ${esc}[7mReverse${esc}[0m` +
                    `${esc}]133;D;7\x07${esc}[?2026l`;
                }
                const encoded = btoa(String.fromCharCode(...new TextEncoder().encode(output)));
                return `exec /bin/sh -c "printf %s '${encoded}' | base64 -d; ${closure === "exit" ? "exit 7" : "sleep 120"}"`;
              }, { scenario, closure });
              await test.evaluate(command => { retained.focus(); retained.paste(command); }, command);
              await test.keyboard.press("Enter");
              stage = "final resources";
              await test.waitForFunction(scenario => scenario === "history"
                ? retained.screenText.split("\n").some(line => line.trim() === "HISTORY_READY")
                : retained.title === "EXOTIC_FINAL_TITLE" && retained.stats.imageCount >= 7, scenario);
              if (scenario === "features" && closure !== "exit")
                await test.waitForFunction(() => decorations.some(message =>
                  message.revision === retained.stats.revision && message.ranges.some(range => range.startColumn >= 9)));
              if (scenario === "history") {
                await test.evaluate(() => retained.scrollLines(-20));
                await test.waitForFunction(() => !retained.viewport.pending && !retained.viewport.following);
                const bounds = await test.locator("#retained canvas:not(.scrollbar-canvas)").boundingBox();
                await test.mouse.move(bounds.x + 5, bounds.y + 10);
                await test.mouse.down();
                await test.mouse.move(bounds.x + 85, bounds.y + 10);
                await test.mouse.up();
                await test.waitForFunction(() => retained.selection.status === "valid");
                await test.evaluate(async () => {
                  window.customMarker = await retained.addMarker({
                    position: { generation: retained.viewport.generation, rowId: retained.viewport.rowIds[0], column: 0 },
                    label: "Retained custom bookmark", color: "#12ab34"
                  });
                });
                check(await test.evaluate(() => retained.selection.text.startsWith("HISTORY-") &&
                  retained.markers.some(marker => marker.id === customMarker.id && marker.label === customMarker.label)),
                `${label}: selection/marker fixture did not establish producer-backed state`);
              }
            } else {
              await test.waitForFunction(() => retained.stats.frames >= 5 && retained.stats.imageCount > 0);
              if (scenario === "animation") {
                const bytes = await test.evaluate(() => retained.stats.workloadBytes);
                const initial = await test.locator("#retained canvas:not(.scrollbar-canvas)").screenshot();
                let advanced = false;
                for (let attempt = 0; attempt < 10 && !advanced; attempt++)
                  advanced = !initial.equals(await test.locator("#retained canvas:not(.scrollbar-canvas)").screenshot());
                check(advanced && await test.evaluate(bytes => retained.stats.workloadBytes === bytes, bytes),
                  `${label}: animation was not advancing without producer output`);
              }
              await test.request.post(`${origin}/api/terminals/${instance}/controls`, {
                headers: { Origin: origin }, data: { paused: true }
              });
              await test.waitForTimeout(250);
            }
            stage = "disconnect and retained redraw";
            const snapshot = () => test.evaluate(() => ({
              text: retained.screenText, geometry: retained.geometry, title: retained.title,
              progress: retained.progress, shell: retained.shellIntegration, directory: retained.workingDirectory,
              mark: retained.commandMark, markers: retained.markers, viewport: retained.viewport,
              selection: retained.selection, stats: retained.stats
            }));
            const before = await snapshot();
            if (closure !== "exit") {
              const response = await test.request.post(`${origin}/api/terminals/${instance}/views/${viewId}/failure`, {
                headers: { Origin: origin }, data: { mode: closure }
              });
              check(response.status() === 202, `${label}: disconnect rejected`);
            }
            await test.waitForFunction(() => !!window.retainedClose && !retained.connected);
            const after = await snapshot();
            check(await test.evaluate(() => retainedClose.code) === (closure === "abort" ? 1006 : closure === "exit" ? 4000 : 1000),
              `${label}: wrong closure`);
            if (scenario === "discard-kgp") {
              check(before.stats.imageCount > 0 && after.stats.imageCount === 0 && after.stats.textureBytes === 0,
                `${label}: opt-out did not release graphics resources`);
              await test.evaluate(() => retained.dispose());
              check(await test.locator("#retained").evaluate(host => !host.children.length), `${label}: disposal retained the view`);
              check(!errors.length, `${label}: ${errors.join("; ")}`);
              results.push({ renderer, transport, scenario, closure, images: after.stats.imageCount, passed: true });
              continue;
            }
            for (const key of ["text", "geometry", "title", "progress", "shell", "directory", "mark", "markers", "viewport", "selection"])
              check(JSON.stringify(before[key], (key, value) => key === "revision" ? undefined : value) ===
                JSON.stringify(after[key], (key, value) => key === "revision" ? undefined : value),
              `${label}: ${key} changed at disconnect`);
            check(after.stats.renderer === renderer && after.stats.gpu === "stopped" && !after.stats.warnings.length,
              `${label}: renderer failed`);
            if (scenario !== "history")
              check(after.stats.imageCount > 0 && after.stats.textureBytes > 0, `${label}: graphics resources were lost`);
            const canvas = test.locator("#retained canvas:not(.scrollbar-canvas)");
            const controlsBefore = controls.length;
            if (scenario === "features") {
              const box = await canvas.boundingBox();
              await test.keyboard.down("Control");
              await test.mouse.click(box.x + 15, box.y + box.height * 4.5 / 24);
              await test.keyboard.up("Control");
              check(await test.evaluate(() => linkActivations === 0), `${label}: stale hyperlink was activated`);
            }
            const finalPixels = await canvas.screenshot();
            if (scenario === "features") {
              check(after.progress.state === "error" && after.progress.percentage === 37 &&
                after.shell.phase === "finished" && after.shell.lastExitCode === 7 &&
                after.directory.path === "/tmp/preserved" && after.mark.exitCode === 7,
              `${label}: activity/OSC metadata was not established`);
              const colors = await test.evaluate(async png => {
                const image = await createImageBitmap(new Blob([new Uint8Array(png)], { type: "image/png" }));
                const canvas = document.createElement("canvas"); canvas.width = image.width; canvas.height = image.height;
                const ctx = canvas.getContext("2d"); ctx.drawImage(image, 0, 0); image.close();
                const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
                return [[255, 0, 0], [0, 255, 0], [255, 0, 255], [0, 255, 255], [255, 128, 0], [0, 0, 255], [128, 0, 255]]
                  .map(rgb => {
                    let count = 0;
                    for (let i = 0; i < pixels.length; i += 4)
                      if (rgb.every((channel, j) => channel === pixels[i + j])) count++;
                    return count;
                  });
              }, [...finalPixels]);
              check(colors.every(count => count > 50), `${label}: missing RGBA/PNG/zlib/relative/placeholder/Sixel/cropped RGB pixels: ${colors}`);
              const metadata = frames.at(-1);
              check(metadata.placements.some(p => p.kind === "sixel") &&
                metadata.placements.filter(p => p.kind === "kgp").length >= 6 &&
                metadata.placements.some(p => p.sourceWidth === 1 &&
                  metadata.retainedImages.includes(p.key)) &&
                metadata.hyperlinks.length && metadata.lineRenditions.includes(2) && metadata.lineRenditions.includes(3),
              `${label}: incomplete exotic final state`);
            }
            await test.waitForTimeout(1400);
            check(finalPixels.equals(await canvas.screenshot()), `${label}: retained framebuffer kept animating`);
            await test.evaluate(() => { retained.setColorMode("light"); });
            let changed = false;
            for (let attempt = 0; attempt < 15 && !changed; attempt++)
              changed = !finalPixels.equals(await canvas.screenshot());
            check(changed, `${label}: disconnected palette redraw failed`);
            await test.evaluate(() => retained.setColorMode("dark"));
            let restored = false;
            for (let attempt = 0; attempt < 15 && !restored; attempt++)
              restored = finalPixels.equals(await canvas.screenshot());
            check(restored, `${label}: palette restore lost final pixels`);
            await test.evaluate(() => { document.getElementById("retained").style.width = "400px"; });
            await test.waitForFunction(() => retained.layout.content.width <= 400);
            await canvas.screenshot();
            await test.evaluate(() => { document.getElementById("retained").style.width = "800px"; });
            await test.waitForFunction(() => retained.layout.content.width > 400);
            restored = false;
            for (let attempt = 0; attempt < 15 && !restored; attempt++)
              restored = finalPixels.equals(await canvas.screenshot());
            check(restored, `${label}: resize restore lost final pixels`);
            check(controls.length === controlsBefore, `${label}: redraw sent controls on a disconnected transport`);
            check(await test.evaluate(() => retained.stats.imageUploadBytes) === after.stats.imageUploadBytes,
              `${label}: redraw uploaded fresh image data`);
            if (scenario === "history") {
              const errors = await test.evaluate(async () => {
                const failures = [];
                for (const action of [() => retained.copySelection(), () => retained.scrollToMarker(customMarker.id)]) {
                  try { await action(); } catch (error) { failures.push(error.message); }
                }
                return failures;
              });
              check(errors.length === 2 && errors.every(error => /not connected|disconnected/i.test(error)),
                `${label}: producer-backed operations did not reject on disconnect`);
              check(controls.length === controlsBefore, `${label}: unavailable history operations sent controls`);
            }
            await test.evaluate(() => retained.dispose());
            check(await test.locator("#retained").evaluate(host => !host.children.length), `${label}: disposal retained the view`);
            check(!errors.length, `${label}: ${errors.join("; ")}`);
            results.push({ renderer, transport, scenario, closure, images: after.stats.imageCount, passed: true });
          } catch (error) {
            const detail = await test.evaluate(() => window.retained && ({
              text: retained.screenText, title: retained.title, progress: retained.progress,
              shell: retained.shellIntegration, directory: retained.workingDirectory, mark: retained.commandMark,
              stats: retained.stats, close: window.retainedClose
            }));
            throw new Error(`${label}/${stage}: ${error.message}; ${JSON.stringify(detail)}`);
          } finally {
            if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
            await context.close();
          }
        }
      }
    }
  }
  return { passed: true, cases: results.length, results };
}
