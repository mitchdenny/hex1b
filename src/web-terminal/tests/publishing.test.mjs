import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { publicationTarget, validateVersion } from "../../../.github/scripts/publish-web-terminal.mjs";

test("Beta packages use the shared version without moving latest", () => {
  const version = "0.166.0-beta.1552.1.abcdef0";
  validateVersion(version, "beta");
  assert.deepEqual(publicationTarget(version, "beta"), {
    registry: "https://registry.npmjs.org",
    tag: "beta"
  });
  assert.throws(() => publicationTarget(version, "release"));
});

test("Stable packages use latest while PR packages remain artifacts only", () => {
  assert.deepEqual(publicationTarget("0.166.0", "release"), {
    registry: "https://registry.npmjs.org",
    tag: "latest"
  });
  assert.throws(() => publicationTarget("0.166.0-pr.525.1552.1.abcdef0", "pr"));
});

test("Stable release tags the exact commit used to build the packages", () => {
  const workflow = readFileSync(new URL("../../../.github/workflows/build-deploy.yml", import.meta.url), "utf8");
  const command = workflow.match(/^\s*gh release create\b(?:[^\n]*\\\n)*[^\n]*/mu)?.[0];
  assert.ok(command, "Deploy must create the stable GitHub release");
  assert.match(command, /--target "\$\{\{ github\.sha \}\}"/u);
});
