async page => {
  // This focused browser contract test needs emitted playground modules, not a producer.
  // The full scrollbars.browser.js fixture exercises the same wrapper with real HWT1 views.
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  await page.goto(`${origin}/?empty=1`);
  const result = await page.evaluate(async () => {
    const { NativeScrollbar } = await import("/playground/native-scrollbar.js");
    const parent = document.createElement("div");
    parent.style.cssText = "display:flex;width:300px;height:400px";
    document.body.append(parent);
    let viewport = { available: true, buffer: "main", totalRows: 20_000_020,
      liveTop: 20_000_000, top: 10_000_000, pending: false };
    const calls = [], jumps = [], errors = [];
    const terminal = {
      get viewport() { return viewport; }, connected: true,
      markers: [{ id: "custom:first", source: "custom", buffer: "main", row: 5, column: 0 }],
      layout: { content: { top: 0, height: 400 }, cellHeight: 20 },
      scrollToRow: row => { calls.push(row); viewport = { ...viewport, pending: true }; },
      scrollToMarker: async id => { jumps.push(id); }
    };
    const wrapper = new NativeScrollbar(terminal, parent, error => errors.push(String(error)));
    try {
      const rail = wrapper.rail, range = rail.scrollHeight - rail.clientHeight;
      const initial = rail.scrollTop;
      rail.scrollTop = range * .75;
      rail.dispatchEvent(new Event("scroll"));
      await new Promise(requestAnimationFrame);
      wrapper.update();
      const pending = rail.scrollTop;
      viewport = { ...viewport, pending: false, top: calls.at(-1) };
      wrapper.update();
      const settled = rail.scrollTop;
      const height = rail.scrollHeight;
      const oldTick = wrapper.element.querySelector(".native-marker");
      oldTick.click();
      oldTick.focus();
      viewport = { ...viewport, totalRows: viewport.totalRows + 1 };
      wrapper.update();
      const retainedTick = wrapper.element.querySelector(".native-marker");
      const retainedFocus = document.activeElement === retainedTick;
      terminal.markers = [{ ...terminal.markers[0], id: "custom:replacement" }];
      wrapper.update();
      const removedFocus = document.activeElement === rail;
      oldTick.click();
      retainedTick.click();
      const replacementTick = wrapper.element.querySelector(".native-marker");
      wrapper.dispose();
      replacementTick.click();
      const before = calls.length;
      rail.scrollTop = 0;
      rail.dispatchEvent(new Event("scroll"));
      await new Promise(requestAnimationFrame);
      return { height, range, initial, pending, settled, calls, jumps, errors, retainedFocus, removedFocus,
        disposed: before === calls.length };
    } finally {
      wrapper.dispose();
      parent.remove();
    }
  });
  const check = (condition, message) => { if (!condition) throw new Error(`${message}: ${JSON.stringify(result)}`); };
  check(result.height <= 8_000_000 && result.range > 0, "Native extent exceeded the safe browser cap");
  check(Math.abs(result.initial / result.range - .5) < .00001, "Large history was not scaled to native pixels");
  check(result.calls.length === 1 && Math.abs(result.calls[0] - 15_000_000) < 10,
    "Native pixels did not map back to absolute rows exactly once");
  check(Math.abs(result.pending - result.settled) < 1 && result.pending > result.initial,
    "Older pending viewport pulled the thumb backwards");
  check(result.disposed && !result.errors.length, "Disposed native wrapper still navigated or reported errors");
  check(result.jumps.length === 1 && result.jumps[0] === "custom:first",
    "Replaced or disposed marker buttons retained navigation listeners");
  check(result.retainedFocus, "Live history updates lost keyboard focus on a retained marker");
  check(result.removedFocus, "An evicted focused marker did not return keyboard focus to the scroll rail");
  return result;
}
