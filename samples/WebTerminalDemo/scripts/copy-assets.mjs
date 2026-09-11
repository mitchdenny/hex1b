import { cp, rm } from "node:fs/promises";

const source = new URL("../../../src/web-terminal/dist/", import.meta.url);
const destination = new URL("../wwwroot/web-terminal/", import.meta.url);

await rm(destination, { recursive: true, force: true });
await cp(source, destination, { recursive: true });
