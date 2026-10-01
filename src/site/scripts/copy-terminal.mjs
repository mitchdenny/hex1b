import { cp, mkdir, rm } from "node:fs/promises";

const source = new URL("../../web-terminal/dist/", import.meta.url);
const destination = new URL("../public/web-terminal/", import.meta.url);
// Resolved, generated assets only; never touch either existing website.
await rm(destination, { recursive: true, force: true });
await mkdir(destination, { recursive: true });
await cp(source, destination, { recursive: true });
