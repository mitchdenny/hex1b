import { cp } from "node:fs/promises";

await cp(new URL("../LICENSE", import.meta.url), new URL("../dist/LICENSE", import.meta.url));
await cp(new URL("../fonts/", import.meta.url), new URL("../dist/fonts/", import.meta.url), { recursive: true });
await cp(new URL("../fonts/", import.meta.url), new URL("../.build/fonts/", import.meta.url), { recursive: true });
