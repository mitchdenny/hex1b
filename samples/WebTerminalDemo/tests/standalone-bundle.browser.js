async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const browser = page.context().browser();
  const check = (value, message) => { if (!value) throw new Error(message); };
  const reports = [];
  for (const path of ["/web-terminal/index.js", "/web-terminal-test/vendor/renamed-terminal.js"]) {
    for (const renderer of ["webgpu", "webgl2"]) {
      const context = await browser.newContext({ viewport: { width: 1100, height: 850 } });
      const test = await context.newPage();
      const errors = [], requests = [];
      let instanceId, stage = `${path}/${renderer}: public-only mount under self-only worker CSP`;
      test.on("pageerror", error => errors.push(error.message));
      test.on("request", request => requests.push(request.url()));
      try {
        const available = await test.request.get(`${origin}${path}`);
        check(available.ok(), `${path} is missing; run npm run test:assets in samples/WebTerminalDemo after building the package`);
        await test.goto(`${origin}/health`);
        await test.setContent(`
          <!doctype html><html><head>
          <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'self'; worker-src 'self'; connect-src 'self' ${origin.replace(/^http/, "ws")}; font-src 'self'; style-src 'unsafe-inline'; img-src 'self' data:">
          <title>Standalone WebTerminal bundle</title>
          </head><body><div id="terminal" style="width:800px;height:480px"></div></body></html>
        `);
        const created = await test.request.post(`${origin}/api/terminals`, {
          headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24 }
        });
        check(created.status() === 201, `Creating standalone shell returned ${created.status()}`);
        instanceId = (await created.json()).id;
        const override = renderer === "webgl2";
        const setup = await test.evaluate(async ({ path, renderer, override, instanceId }) => {
          window.bundleWorkers = [];
          window.bundleViolations = [];
          window.bundleLinkErrors = [];
          window.bundleActivations = [];
          document.addEventListener("securitypolicyviolation", event =>
            bundleViolations.push({ directive: event.violatedDirective, blocked: event.blockedURI }));
          const NativeWorker = window.Worker;
          window.Worker = class extends NativeWorker {
            constructor(url, options) {
              super(url, options);
              this.fixtureRecord = { url: new URL(url, location.href).href, type: options?.type, terminated: false, messages: 0 };
              bundleWorkers.push(this.fixtureRecord);
              this.addEventListener("message", () => {
                this.fixtureRecord.messages++;
              });
            }
            terminate() {
              this.fixtureRecord.terminated = true;
              return super.terminate();
            }
          };
          const entry = new URL(path, location.href);
          entry.searchParams.set("vendored", "bundle-check");
          const api = await import(entry.href);
          const mainRoleImport = await import(`${entry.href}#hex1b-terminal-worker`);
          if (typeof api.WebTerminal !== "function" || typeof mainRoleImport.WebTerminal !== "function" ||
              bundleWorkers.length !== 0) throw new Error("Importing the public API in a window bootstrapped a worker role");
          const terminalWorker = new URL(entry), detectionWorker = new URL(entry);
          terminalWorker.hash = "hex1b-terminal-worker";
          detectionWorker.hash = "hex1b-link-detection-worker";
          if (override) {
            terminalWorker.searchParams.set("override", "terminal");
            detectionWorker.searchParams.set("override", "links");
          }
          window.bundleTerminal = await api.WebTerminal.mount(document.getElementById("terminal"), {
            url: `/ws?instance=${instanceId}&name=Standalone%20bundle`,
            renderer, sizing: { mode: "fixed", columns: 80, rows: 24 },
            ...(override ? { workerUrl: terminalWorker, linkDetectionWorkerUrl: detectionWorker } : {}),
            links: {
              osc8: false,
              detection: {
                rules: [{ id: "standalone-url", builtin: "url", action: (_context, link) => bundleActivations.push(link) }]
              }
            },
            onLinkDetectionError: error => bundleLinkErrors.push(error)
          });
          bundleTerminal.requestPrimary();
          return { entry: entry.href, terminalWorker: terminalWorker.href, detectionWorker: detectionWorker.href,
            font: new URL("./fonts/cascadia-mono-nf/CascadiaMonoNF.woff2", entry).href };
        }, { path, renderer, override, instanceId });
        await test.waitForFunction(() => bundleTerminal.peer.isPrimary && bundleTerminal.geometry.columns === 80 &&
          bundleTerminal.geometry.rows === 24 && /([#$%>]|[^\x00-\x7f])$/.test(bundleTerminal.screenText.trimEnd()));
        const initial = await test.evaluate(() => ({
          renderer: bundleTerminal.stats.renderer, font: bundleTerminal.stats.fontFamily,
          fallback: bundleTerminal.stats.rendererFallbackReason
        }));
        check(initial.renderer === renderer && !initial.fallback, `Forced renderer did not execute: ${JSON.stringify(initial)}`);
        check(initial.font === "Cascadia Mono NF", `Bundled default font was not loaded: ${initial.font}`);

        stage = `${path}/${renderer}: real rendering and isolated inferred-link worker`;
        const target = "https://example.test/standalone-bundle";
        await test.evaluate(() => bundleTerminal.focus());
        // Clearing a row makes its left logical-line boundary unknown; keep the URL inside it.
        await test.keyboard.type(`printf '\\033[2J\\033[HURL: ${target}\\nPUBLIC-BUNDLE-READY\\n'`);
        await test.keyboard.press("Enter");
        stage = `${path}/${renderer}: shell output rendered`;
        await test.waitForFunction(target => bundleTerminal.screenText.split("\n")[0] === `URL: ${target}` &&
          bundleTerminal.screenText.includes("PUBLIC-BUNDLE-READY"), target);
        const point = await test.evaluate(() => {
          const box = bundleTerminal.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)").getBoundingClientRect();
          return { x: box.x + 6.5 * box.width / 80, y: box.y + .5 * box.height / 24 };
        });
        await test.mouse.move(point.x, point.y);
        stage = `${path}/${renderer}: inferred-link hover`;
        await test.waitForFunction(target =>
          bundleTerminal.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)").getAttribute("title")?.includes(target), target);
        await test.keyboard.down("Control");
        await test.mouse.click(point.x, point.y);
        await test.keyboard.up("Control");
        stage = `${path}/${renderer}: inferred-link activation`;
        await test.waitForFunction(target => bundleActivations.some(link =>
          link.source === "detected" && link.ruleId === "standalone-url" && link.target === target), target);
        const workers = await test.evaluate(() => bundleWorkers);
        check(workers.some(worker => worker.url === setup.terminalWorker) &&
          workers.some(worker => worker.url === setup.detectionWorker),
        `Workers lost the relocated path, query, explicit override, or role fragment: ${JSON.stringify(workers)}`);
        check(workers.every(worker => worker.messages > 0 && worker.type === "module" &&
          worker.url.startsWith(`${origin}/`) && !worker.url.startsWith("blob:")),
        "Standalone package did not use same-origin module workers");
        check(requests.includes(setup.font), `Default font did not load beside the bundle: ${setup.font}`);
        const javascript = requests.filter(url => /\.js(?:[?#]|$)/.test(url));
        check(javascript.length > 0 && javascript.every(url =>
          url.split("?")[0].split("#")[0] === `${origin}${path}`),
        `Standalone package requested a legacy/private JavaScript module: ${JSON.stringify(javascript)}`);
        const diagnostics = await test.evaluate(() => ({
          csp: bundleViolations, links: bundleLinkErrors,
          rendered: bundleTerminal.stats.presentations, text: bundleTerminal.screenText
        }));
        check(diagnostics.csp.length === 0 && diagnostics.links.length === 0 && diagnostics.rendered > 0,
          `CSP or worker isolation failed: ${JSON.stringify(diagnostics)}`);
        const canvas = test.locator("#terminal canvas:not(.scrollbar-canvas)");
        check(await canvas.isVisible() && (await canvas.screenshot()).byteLength > 500,
          "The selected backend did not render a visible terminal canvas");
        await test.evaluate(() => bundleTerminal.dispose());
        check(await test.evaluate(() => bundleWorkers.every(worker => worker.terminated) &&
          document.getElementById("terminal").childElementCount === 0), "Bundle disposal leaked a worker or terminal DOM");
        check(errors.length === 0, errors.join("; "));
        reports.push({ path, renderer, override, font: initial.font, workers: await test.evaluate(() => bundleWorkers),
          javascriptFiles: [...new Set(javascript.map(url => url.split("?")[0].split("#")[0]))],
          selfOnlyWorkerCsp: true, inferredLinkActivated: true });
      } catch (error) {
        const state = await test.evaluate(() => ({
          text: window.bundleTerminal?.screenText, workers: window.bundleWorkers,
          links: window.bundleLinkErrors, csp: window.bundleViolations,
          canvas: (() => { const c = window.bundleTerminal?.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)");
            return c && { box: c.getBoundingClientRect().toJSON(), title: c.title, width: c.width, height: c.height }; })()
        })).catch(() => null);
        throw new Error(`${stage}: ${error.message}; errors=${JSON.stringify(errors)}; state=${JSON.stringify(state)}`);
      } finally {
        await test.keyboard.up("Control").catch(() => {});
        try {
          if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
        } finally {
          await context.close();
        }
      }
    }
  }
  return { passed: true, reports };
}
