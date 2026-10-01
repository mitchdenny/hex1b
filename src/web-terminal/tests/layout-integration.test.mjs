import assert from "node:assert/strict";
import { test } from "node:test";
import { browser, mounting, present } from "./fixtures/browser.mjs";

test("Live padding and beside controls preserve the worker and request only primary grid changes", async t => {
  const observers = [];
  const layouts = [];
  const workers = browser(t, {
    ResizeObserver: class {
      constructor(callback) { observers.push(callback); }
      observe() {}
      disconnect() {}
    }
  });
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const view = await mounting(t, workers, { onLayoutChange: layout => layouts.push(layout) });
  observers[0]([{ contentRect: { width: 800, height: 480 } }]);
  await view.worker.request("open");
  await present(view, { columns: 80, rows: 24 });
  const terminal = await view.promise;
  assert.equal(terminal.layout.content.width, 800);
  terminal.setPadding({ top: 10, right: 20, bottom: 30, left: 40 });
  terminal.setScrollbar({ placement: "beside", width: 12 });
  t.mock.timers.tick(50);
  await view.worker.request("flush");
  assert.equal(workers.length, 1, "Layout changes never remount the worker");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "resize").at(-1),
    { type: "resize", columns: 72, rows: 22 });
  assert.deepEqual(terminal.padding, { top: 10, right: 20, bottom: 30, left: 40 });
  assert.equal(terminal.scrollbar.placement, "beside");
  assert.equal(terminal.layout.scrollbar.width, 12);
  assert.ok(layouts.length > 0 && layouts.at(-1) === terminal.layout);
  assert.ok(Object.isFrozen(terminal.layout));
  terminal.setScrollbar(false);
  assert.equal(terminal.layout.scrollbar, null);
  assert.equal(terminal.scrollbar, false);
  terminal.dispose();
  assert.throws(() => terminal.setPadding(0), /disposed/);
  assert.throws(() => terminal.setScrollbar(false), /disposed/);
  assert.throws(() => terminal.refreshScrollbar(), /disposed/);
});

test("Secondary padding and gutter changes only refit displayed content", async t => {
  const observers = [];
  const workers = browser(t, {
    ResizeObserver: class {
      constructor(callback) { observers.push(callback); }
      observe() {}
      disconnect() {}
    }
  });
  const view = await mounting(t, workers);
  observers[0]([{ contentRect: { width: 400, height: 240 } }]);
  await view.worker.request("open");
  await present(view, { columns: 80, rows: 24, peer: { id: "secondary", primaryId: "native", isPrimary: false } });
  const terminal = await view.promise;
  terminal.setScrollbar({ placement: "beside", width: 16 });
  terminal.setPadding(8);
  await view.worker.request("flush");
  assert.equal(terminal.layout.content.width, 368);
  assert.equal(terminal.layout.cellWidth, 4.6);
  assert.equal(terminal.geometry.columns, 80);
  assert.deepEqual(view.worker.commands.filter(command => ["resize", "requestPrimary"].includes(command.type)), []);
  terminal.dispose();
});

test("Invalid live layout options leave previous normalized state untouched", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers, { padding: 8 });
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  const original = terminal.layout;
  assert.throws(() => terminal.setPadding({ left: -1 }));
  assert.throws(() => terminal.setScrollbar({ placement: "outside" }));
  assert.equal(terminal.layout, original);
  assert.equal(terminal.padding.left, 8);
  assert.equal(terminal.scrollbar.placement, "overlay");
  terminal.dispose();
});
