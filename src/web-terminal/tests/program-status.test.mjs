import assert from "node:assert/strict";
import { test } from "node:test";
import { validateProgramStatus, programStatusSnapshot, sameProgramStatus } from "../.build/program-status.js";
import { browser, mounting, present } from "./fixtures/browser.mjs";

const record = (id = "", overrides = {}) => ({
  id, state: "working", kind: null, progress: null, app: null,
  effectiveApp: null, title: null, message: null, ...overrides,
});

test("Program status validates every semantic state and supported blocked kind", () => {
  for (const state of ["idle", "working", "blocked", "done", "error"]) {
    validateProgramStatus({ records: [record("", { state })] });
    for (const progress of [0, 100]) {
      const inventory = { records: [record("", { state, progress })] };
      if (state === "working" || state === "blocked") validateProgramStatus(inventory);
      else assert.throws(() => validateProgramStatus(inventory), /progress/u);
    }
    for (const kind of ["permission", "question", "auth"]) {
      const inventory = { records: [record("", { state, kind })] };
      if (state === "blocked") validateProgramStatus(inventory);
      else assert.throws(() => validateProgramStatus(inventory), /kind/u);
    }
  }
});

test("Program status validates bounded IDs, inventory and Unicode text", () => {
  for (const id of ["", "a", "a".repeat(32), "A_1.+-/b", Array(8).fill("a").join("/"),
    ["a".repeat(32), "b".repeat(32), "c".repeat(32), "d".repeat(29)].join("/")])
    validateProgramStatus({ records: [record(id)] });
  for (const id of ["/", "/a", "a/", "a//b", "a b", "ä", "a".repeat(33),
    Array(9).fill("a").join("/"), Array(4).fill("a".repeat(32)).join("/")])
    assert.throws(() => validateProgramStatus({ records: [record(id)] }), /ID/u);
  validateProgramStatus({ records: Array.from({ length: 256 }, (_, i) => record(String(i))) });
  assert.throws(() => validateProgramStatus({
    records: Array.from({ length: 257 }, (_, i) => record(String(i))),
  }), /inventory/u);
  assert.throws(() => validateProgramStatus({ records: [record(), record()] }), /duplicate/u);
  for (const [field, limit] of [["title", 192], ["message", 2048]]) {
    for (const value of ["", "<script>alert(1)</script>", "日本語 😀", "a".repeat(limit), "😀".repeat(limit / 4)])
      validateProgramStatus({ records: [record("", { [field]: value })] });
    for (const value of ["a".repeat(limit + 1), "😀".repeat(limit / 4 + 1), "\ud800", "\udfff",
      ...Array.from({ length: 32 }, (_, i) => String.fromCharCode(i)),
      ...Array.from({ length: 33 }, (_, i) => String.fromCharCode(127 + i))])
      assert.throws(() => validateProgramStatus({ records: [record("", { [field]: value })] }), /text/u);
  }
});

test("Program status rejects malformed fields instead of manufacturing safe-looking state", () => {
  for (const inventory of [null, [], {}, { records: null }, { records: [null] }])
    assert.throws(() => validateProgramStatus(inventory));
  for (const [field, values] of [
    ["state", [null, 0, {}, "clear", "unknown"]],
    ["kind", [undefined, 0, "other"]],
    ["progress", [undefined, -1, 101, 1.5, "50"]],
    ["app", [undefined, "", "two words", "ä", "a".repeat(33)]],
    ["effectiveApp", [undefined, "", "ä"]],
    ["message", [undefined, 1, {}]],
    ["title", [undefined, 1, {}]],
  ]) for (const value of values)
    assert.throws(() => validateProgramStatus({ records: [record("", { [field]: value })] }));
});

test("Program status inheritance uses the nearest existing ancestor and exact path boundaries", () => {
  const inventory = { records: [
    record("deploy/east/upload", { effectiveApp: "regional" }),
    record("", { app: "root-app", effectiveApp: "root-app" }),
    record("deploy", { app: "deploy-app", effectiveApp: "deploy-app" }),
    record("deploy/east", { app: "regional", effectiveApp: "regional" }),
    record("deployment", { effectiveApp: "root-app" }),
  ] };
  validateProgramStatus(inventory);
  const status = programStatusSnapshot(inventory);
  assert.equal(status.find("deploy/east/upload").effectiveApp, "regional");
  assert.equal(status.find("missing"), undefined);
  assert.deepEqual(status.getDescendants("deploy").map(r => r.id), ["deploy/east/upload", "deploy/east"]);
  assert.equal(status.getDescendants("").length, 4);
  assert.deepEqual(status.getDescendants("missing"), []);
  assert.throws(() => validateProgramStatus({ records: [record("deploy/east", { effectiveApp: "fake" })] }),
    /inheritance/u);
});

test("Program status snapshots own frozen records and preserve old inventories", () => {
  const inventory = { records: [record("", { message: "old" })] };
  const snapshot = programStatusSnapshot(inventory);
  inventory.records[0].message = "new";
  inventory.records.push(record("child"));
  assert.equal(snapshot.records.length, 1);
  assert.equal(snapshot.find("").message, "old");
  assert.throws(() => { snapshot.records.push(record()); }, TypeError);
  assert.throws(() => { snapshot.find("").message = "mutated"; }, TypeError);
  assert.throws(() => { snapshot.records = []; }, TypeError);
  assert.equal(sameProgramStatus(snapshot, programStatusSnapshot({ records: [record("", { message: "old" })] })), true);
  assert.equal(sameProgramStatus(snapshot, inventory), false);
});

test("Presented program status delivers a baseline, distinct updates and empty clears before mount", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, {
    onProgramStatusChange(status) {
      assert.equal(view.handle.programStatus, status);
      notices.push(status);
    },
  });
  await view.worker.request("open");
  const inventory = { records: [record("", { app: "demo", effectiveApp: "demo" }),
    record("east", { state: "blocked", kind: "permission", effectiveApp: "demo", message: "Approve?" })] };
  await present(view, { programStatus: inventory });
  const terminal = await view.promise;
  assert.equal(notices.length, 1);
  assert.equal(terminal.programStatus.find("east").kind, "permission");
  await present(view, { revision: 2, programStatus: inventory });
  assert.equal(notices.length, 1);
  await present(view, { revision: 3, programStatus: { records: [] } });
  assert.equal(notices.length, 2);
  assert.equal(terminal.programStatus.records.length, 0);
  assert.equal(notices[0].records.length, 2);
  assert.deepEqual(view.worker.errors, []);
});

test("Program status rejects corrupt frames and retains the last snapshot on disconnect/disposal", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, { onProgramStatusChange(status) { notices.push(status); } });
  await view.worker.request("open");
  await present(view, { programStatus: { records: [record("", { state: "done", message: "Finished" })] } });
  const terminal = await view.promise;
  const before = terminal.programStatus;
  await present(view, { revision: 2, programStatus: { records: [record("", { state: "clear" })] } });
  assert.equal(terminal.connected, false);
  assert.equal(terminal.programStatus, before);
  terminal.dispose();
  assert.equal(terminal.programStatus, before);
  assert.equal(notices.length, 1);
});

test("Disposing in program status callback suppresses subsequent activity callbacks", async t => {
  const workers = browser(t);
  let shellChanges = 0;
  const view = await mounting(t, workers, {
    onProgramStatusChange(status) {
      if (status.find("")?.state === "error") view.handle.dispose();
    },
    onShellIntegrationChange() { shellChanges++; },
  });
  await view.worker.request("open");
  await present(view, {});
  await view.promise;
  const geometry = view.worker.outputs.find(message => message.type === "geometry");
  view.worker.deliver({ ...geometry, revision: 2,
    programStatus: { records: [record("", { state: "error" })] },
    shellIntegration: { phase: "finished", lastExitCode: 1 } });
  assert.equal(shellChanges, 1);
});
