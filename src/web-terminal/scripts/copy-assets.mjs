import { cp } from "node:fs/promises";

await cp(new URL("../fonts/", import.meta.url), new URL("../dist/fonts/", import.meta.url), { recursive: true });
