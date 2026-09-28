async page => {
  // Serve src/web-terminal as the HTTP root after npm run build.
  const origin = page.url().match(/^https?:\/\/[^/]+/)[0];
  const context = await page.context().browser().newContext();
  const test = await context.newPage();
  const errors = [];
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/tests/`);
    const reports = await test.evaluate(async () => {
      const { TerminalRenderer } = await import("/.build/renderer.js");
      const check = (value, message) => { if (!value) throw new Error(message); };
      const reports = [];
      const backends = ["webgl2", "webgpu"];
      for (const backend of backends) for (const scale of [1, 1.25, 1.5, 2]) {
        for (const font of [
          { family: "Cascadia Mono NF", faces: [{ url: "/dist/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2", weight: "200 700" }] },
          { family: "monospace" }
        ]) {
          const canvas = new OffscreenCanvas(1, 1);
          const renderer = await TerminalRenderer.create(canvas, scale, error => { throw error; }, font, backend);
          try {
            renderer.resize(12, 4);
            const cells = Array.from({ length: 48 }, (_, index) => ({
              index, text: " ", width: 1, attributes: 0, underlineStyle: 0,
              foreground: 0xffffffff, background: 0xff000000, underlineColor: 0xff0000ff
            }));
            for (const row of [0, 1]) {
              cells[row * 12].text = "H";
              cells[row * 12 + 1].text = "e\u0301";
              cells[row * 12 + 2].text = "界";
              cells[row * 12 + 2].width = 2;
              cells[row * 12 + 3].text = "";
              cells[row * 12 + 3].width = 0;
              cells[row * 12 + 4].text = "👩‍💻";
              cells[row * 12 + 4].width = 2;
              cells[row * 12 + 5].text = "";
              cells[row * 12 + 5].width = 0;
              cells[row * 12].underlineStyle = 2;
            }
            renderer.prepareGlyphs(cells);
            const metadata = { placements: [], lineRenditions: [2, 3, 0, 0],
              cursor: { visible: false }, history: null };
            const read = () => {
              renderer.render(cells, metadata, true);
              const copy = new OffscreenCanvas(canvas.width, canvas.height);
              const ctx = copy.getContext("2d");
              ctx.drawImage(canvas, 0, 0);
              return ctx.getImageData(0, 0, copy.width, copy.height);
            };
            const pixels = read();
            const count = (top, bottom, predicate) => {
              let hits = 0;
              for (let y = Math.ceil(top * scale); y < Math.floor(bottom * scale); y++)
                for (let x = 0; x < pixels.width; x++) {
                  const offset = (y * pixels.width + x) * 4;
                  if (predicate(pixels.data[offset], pixels.data[offset + 1], pixels.data[offset + 2])) hits++;
                }
              return hits;
            };
            const ink = (r, g, b) => r > 80 || g > 80 || b > 80;
            const red = (r, g, b) => r > 180 && g < 50 && b < 50;
            check(count(0, 20, ink) > 50, `${backend}/${scale}: top glyph half missing`);
            check(count(20, 40, ink) > 50, `${backend}/${scale}: bottom glyph half missing`);
            check(count(0, 20, red) === 0, `${backend}/${scale}: underline leaked into top half`);
            check(count(30, 40, red) > 10, `${backend}/${scale}: bottom underline missing`);
            check(count(40, 80, ink) === 0, `${backend}/${scale}: glyphs leaked outside rows`);
            const before = renderer.metrics();
            for (let frame = 0; frame < 100; frame++) {
              metadata.lineRenditions = frame % 2 ? [2, 3, 0, 0] : [1, 1, 0, 0];
              renderer.prepareGlyphs(cells);
              renderer.render(cells, metadata, true);
            }
            await renderer.idle();
            const after = renderer.metrics();
            check(before.glyphUploadBytes === after.glyphUploadBytes &&
              before.atlasBytes === after.atlasBytes && before.atlasGlyphs === after.atlasGlyphs,
            `${backend}/${scale}: mode changes grew glyph resources`);
            reports.push({ backend, scale, font: after.fontFamily, glyphs: after.atlasGlyphs,
              topPixels: count(0, 20, ink), bottomPixels: count(20, 40, ink) });
          } finally {
            renderer.dispose();
            check(renderer.glyphs.size === 0 && renderer.images.size === 0, "Renderer disposal retained resources");
          }
        }
      }
      return reports;
    });
    for (const scale of [1, 1.25, 1.5, 2]) {
      await test.evaluate(async scale => {
        const { captureMouse } = await import("/.build/mouse-input.js");
        document.body.innerHTML = `<canvas width="120" height="80" style="width:${120 * scale}px;height:${80 * scale}px"></canvas>`;
        const canvas = document.querySelector("canvas");
        const fixture = { modes: [2, 3, 0, 0], historical: true, selections: [], commands: [], links: [] };
        const capture = captureMouse(canvas, command => fixture.commands.push(command), () => {}, {
          lineRenditions: () => fixture.modes,
          state: () => ({ historical: fixture.historical, readOnly: false }),
          begin: point => fixture.selections.push(point),
          hyperlink: point => point.x === 2 && point.y === 1
            ? { id: "link", target: "https://example.com", activation: "click" } : null,
          openHyperlink: link => fixture.links.push(link.id)
        });
        capture.update(12, 4, 1003);
        fixture.capture = capture;
        window.renditionInput = fixture;
      }, scale);
      const bounds = await test.locator("canvas").boundingBox();
      await test.mouse.click(bounds.x + 44 * scale, bounds.y + 8 * scale);
      await test.mouse.move(bounds.x + 44 * scale, bounds.y + 28 * scale);
      if (!(await test.locator("canvas").getAttribute("title")).includes("example.com"))
        throw new Error(`Scaled hyperlink hit test failed at ${scale}`);
      await test.mouse.click(bounds.x + 44 * scale, bounds.y + 28 * scale);
      await test.evaluate(() => {
        const f = window.renditionInput;
        if (f.selections[0]?.x !== 2 || f.selections[0]?.y !== 0 || f.links[0] !== "link" || f.commands.length)
          throw new Error("Historical enlarged input did not use logical coordinates");
        f.modes = [0, 0, 0, 0];
        f.capture.refresh();
      });
      if ((await test.locator("canvas").getAttribute("title")).includes("example.com"))
        throw new Error("Changing rendition left a stale hovered hyperlink");
      await test.evaluate(() => { window.renditionInput.historical = false; });
      await test.mouse.click(bounds.x + 44 * scale, bounds.y + 8 * scale);
      await test.evaluate(() => {
        const f = window.renditionInput;
        if (!f.commands.some(command => command.x === 4 && command.y === 0))
          throw new Error("Normal live input kept enlarged coordinates");
        f.capture.dispose();
        delete window.renditionInput;
      });
    }
    if (errors.length) throw new Error(errors.join("; "));
    return { passed: true, reports, pointerScales: [1, 1.25, 1.5, 2] };
  } finally { await context.close(); }
}
