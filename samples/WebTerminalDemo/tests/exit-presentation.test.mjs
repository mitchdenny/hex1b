import test from "node:test";
import assert from "node:assert/strict";
import { workloadExitCode, exitMessage } from "../wwwroot/playground/exit-presentation.js";

for (const code of [0, 7, -1, -2147483648, 2147483647]) {
  test(`Process exit ${code} is numeric, not WebSocket code 4000`, () => {
    assert.equal(workloadExitCode({ code: 4000, reason: `Workload exited with code ${code}`, wasClean: true }), code);
  });
}

for (const [code, reason] of [
  [1000, "Workload exited with code 0"], [1006, ""], [4000, "Stopped by owner"],
  [4000, "Workload exited with code 1.5"], [4000, "Workload exited with code 2147483648"],
  [4000, "Workload exited with code -2147483649"], [4000, "Workload exited with code NaN"],
  [4000, "<script>alert(0)</script>"], [4000, "Workload exited with code 7 trailing text"]
]) {
  test(`Closure ${code}/${reason} does not fabricate an exit code`, () => {
    assert.equal(workloadExitCode({ code, reason, wasClean: code !== 1006 }), undefined);
  });
}

test("Friendly messages distinguish success, error and retention choices", () => {
  assert.equal(exitMessage(0, true).title, "Finished successfully");
  assert.equal(exitMessage(7, true).title, "The program exited with an error");
  assert.match(exitMessage(7, true).summary, /View final output/);
  assert.match(exitMessage(0, false).summary, /not retained/);
});
