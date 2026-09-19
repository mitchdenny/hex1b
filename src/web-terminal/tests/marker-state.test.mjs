import assert from "node:assert/strict";
import { test } from "node:test";
import { MarkerState } from "../.build/marker-state.js";
import { validateHistory } from "../.build/protocol.js";

const marker = { id: "command:1", source: "command", buffer: "main", row: 3, column: 2, phase: "executing", exitCode: null };
const history = overrides => ({
  generation: "1", buffer: "main", totalRows: 12, liveTop: 10, top: 10, following: true,
  requestId: 0, rowIds: ["11", "12"], selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
  copy: null, markers: [marker], markerResult: null, ...overrides
});

test("Retained markers preserve identities, reject stale revisions, and publish immutable snapshots", () => {
  let changes = 0;
  const state = new MarkerState(() => {}, () => changes++);
  state.accept(history(), 1);
  assert.equal(changes, 1);
  assert.deepEqual(state.markers, [marker]);
  assert.ok(Object.isFrozen(state.markers) && Object.isFrozen(state.markers[0]));
  state.accept(history(), 2);
  assert.equal(changes, 1);
  state.accept(history({ markers: [{ ...marker, row: 7 }] }), 3);
  state.accept(history({ markers: [] }), 2);
  assert.equal(state.markers[0].row, 7);
  state.accept(history({ markers: [{ ...marker, row: null }] }), 4);
  assert.equal(state.markers[0].row, null);
});

test("Marker operations are serialized and each acknowledgement settles its own promise", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const jump = state.jump(marker.id);
  const details = state.details(marker.id);
  assert.equal(commands.length, 1);
  assert.equal(commands[0].action, "jump");
  state.accept(history({ markerResult: { requestId: 1, success: true, markerId: marker.id } }), 1);
  await jump;
  assert.equal(commands.length, 2);
  assert.equal(commands[1].action, "details");
  const value = { phase: "executing", exitCode: null, rawParameters: "cmdline_url=echo%20hello" };
  state.accept(history({ markerResult: { requestId: 2, success: true, details: value } }), 2);
  assert.deepEqual(await details, value);
  assert.ok(Object.isFrozen(await details));
});

test("Custom labels remain client-side and registration returns the authoritative marker", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const registration = state.add({ position: { generation: "1", rowId: "11", column: 2 }, label: "<b>bookmark</b>" });
  const command = commands[0];
  assert.match(command.id, /^custom:/);
  assert.equal(command.label, undefined);
  const custom = { id: command.id, source: "custom", buffer: "main", row: 10, column: 2 };
  state.accept(history({ markers: [custom], markerResult: { requestId: 1, success: true, markerId: command.id } }), 1);
  assert.deepEqual(await registration, { ...custom, label: "<b>bookmark</b>" });
  const removal = state.remove(command.id);
  state.accept(history({ markers: [], markerResult: { requestId: 2, success: true } }), 2);
  await removal;
  assert.deepEqual(state.markers, []);
});

test("Marker errors reject explicitly and do not block later operations", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const jump = assert.rejects(state.jump(marker.id), /evicted/);
  const remove = state.remove("custom:bookmark");
  state.accept(history({ markerResult: { requestId: 1, success: false, error: "The marker was evicted" } }), 1);
  await jump;
  assert.equal(commands.length, 2);
  state.accept(history({ markerResult: { requestId: 2, success: true } }), 2);
  await remove;
});

test("Producer collection releases labels while pending registrations and inactive markers retain theirs", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const jump = state.jump(marker.id);
  const registration = state.add({
    position: { generation: "1", rowId: "11", column: 0 }, label: "Retained bookmark"
  });
  state.accept(history(), 1);
  state.accept(history({ markerResult: { requestId: 1, success: true } }), 2);
  await jump;
  const id = commands[1].id;
  const custom = { id, source: "custom", buffer: "main", row: 10, column: 0 };
  state.accept(history(), 3);
  state.accept(history({
    markers: [custom], markerResult: { requestId: 2, success: true, markerId: id }
  }), 4);
  assert.equal((await registration).label, "Retained bookmark");
  state.accept(history({ markers: [{ ...custom, row: null }] }), 5);
  assert.equal(state.markers[0].label, "Retained bookmark");
  state.accept(history({ markers: [] }), 6);
  assert.deepEqual(state.markers, []);
  // A deliberately repeated server ID cannot resurrect metadata from a collected marker.
  state.accept(history({ markers: [custom] }), 7);
  assert.equal(state.markers[0].label, undefined);
});

test("Markers support the insertion boundary after a maximum-width terminal row", async () => {
  const boundary = { ...marker, column: 1024 };
  validateHistory(history({ markers: [boundary] }), 1024, 2);
  assert.throws(() => validateHistory(history({ markers: [{ ...boundary, column: 1025 }] }), 1024, 2));
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  await assert.rejects(state.add({ position: { generation: "1", rowId: "11", column: 1025 } }), /valid presented/);
  const registration = state.add({ position: { generation: "1", rowId: "11", column: 1024 } });
  const custom = { id: commands[0].id, source: "custom", buffer: "main", row: 10, column: 1024 };
  state.accept(history({ markers: [custom], markerResult: { requestId: 1, success: true, markerId: custom.id } }), 1);
  assert.equal((await registration).column, 1024);
});

test("Disconnect rejects in-flight and queued operations, and invalid input sends nothing", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  for (const id of ["", "1", "command:", "custom:<script>", "command:" + "x".repeat(101)])
    await assert.rejects(state.jump(id), /Invalid terminal marker/);
  await assert.rejects(state.add({ position: { generation: "0", rowId: "1", column: 0 } }), /valid presented/);
  assert.equal(commands.length, 0);
  const first = assert.rejects(state.jump(marker.id), /disconnected/);
  const second = assert.rejects(state.details(marker.id), /disconnected/);
  state.disconnect();
  await Promise.all([first, second]);
});

test("A missing marker response times out all dependent operations without sending them", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const first = assert.rejects(state.jump(marker.id), /Timed out/);
  const second = assert.rejects(state.details(marker.id), /Timed out/);
  t.mock.timers.tick(10000);
  await Promise.all([first, second]);
  assert.equal(commands.length, 1);
  state.disconnect();
});

test("A marker operation timeout preserves labels on previously registered bookmarks", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const registration = state.add({
    position: { generation: "1", rowId: "11", column: 0 }, label: "Keep this bookmark"
  });
  const custom = { id: commands[0].id, source: "custom", buffer: "main", row: 10, column: 0 };
  state.accept(history({
    markers: [custom], markerResult: { requestId: 1, success: true, markerId: custom.id }
  }), 1);
  await registration;
  const timedOut = assert.rejects(state.details(marker.id), /Timed out/);
  t.mock.timers.tick(10000);
  await timedOut;
  state.accept(history({ markers: [custom] }), 2);
  assert.equal(state.markers[0].label, "Keep this bookmark");
  state.disconnect();
});

test("Late replies cannot settle a newer marker operation", async () => {
  const commands = [];
  const state = new MarkerState(command => commands.push(command), () => {});
  const disconnected = assert.rejects(state.jump(marker.id), /disconnected/);
  state.disconnect();
  await disconnected;
  let settled = false;
  const next = state.jump(marker.id).then(() => { settled = true; });
  state.accept(history({ markerResult: { requestId: 1, success: true } }), 1);
  await Promise.resolve();
  assert.equal(settled, false);
  state.accept(history({ markerResult: { requestId: 2, success: true } }), 2);
  await next;
  assert.equal(settled, true);
});

test("History validation rejects malformed marker state and navigation results", () => {
  validateHistory(history(), 10, 2);
  validateHistory(history({ markers: [{ ...marker, row: null, buffer: "alternate" }] }), 10, 2);
  for (const change of [{ id: "1" }, { row: 12 }, { row: -1 }, { column: -1 },
    { buffer: "alternate" }, { source: "custom" }, { phase: "bad" }, { exitCode: 0 }])
    assert.throws(() => validateHistory(history({ markers: [{ ...marker, ...change }] }), 10, 2));
  assert.throws(() => validateHistory(history({ markers: [marker, marker] }), 10, 2));
  for (const result of [{ requestId: 0, success: true }, { requestId: 1, success: "yes" },
    { requestId: 1, success: true, details: {} }, { requestId: 1, success: false, error: 42 }])
    assert.throws(() => validateHistory(history({ markerResult: result }), 10, 2));
});
