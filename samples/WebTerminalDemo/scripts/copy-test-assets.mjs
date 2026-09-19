import { access, cp, mkdir, rm } from "node:fs/promises";

const compiled = new URL("../../../src/web-terminal/.build/", import.meta.url);
const distribution = new URL("../../../src/web-terminal/dist/", import.meta.url);
const destination = new URL("../wwwroot/web-terminal-test/", import.meta.url);

for (const required of [
  new URL("renderer.js", compiled),
  new URL("protocol.js", compiled),
  new URL("index.js", distribution),
  new URL("fonts/", distribution)
]) {
  try {
    await access(required);
  } catch (cause) {
    throw new Error("Build src/web-terminal before running the opt-in demo test:assets command.", { cause });
  }
}

await rm(destination, { recursive: true, force: true });
await cp(compiled, destination, { recursive: true });
await cp(new URL("fonts/", distribution), new URL("fonts/", destination), { recursive: true });

// A renamed copy exercises vendoring without making private modules production assets.
const vendor = new URL("vendor/", destination);
await mkdir(vendor, { recursive: true });
await cp(new URL("index.js", distribution), new URL("renamed-terminal.js", vendor));
await cp(new URL("fonts/", distribution), new URL("fonts/", vendor), { recursive: true });
