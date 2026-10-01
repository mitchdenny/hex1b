import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { writeSampleRepository } from "./sample-repository.mjs";

const publicDirectory = fileURLToPath(new URL("../public/", import.meta.url));
const manifestPath = new URL("../public/recordings/sample.json", import.meta.url);
const sample = JSON.parse(await readFile(manifestPath, "utf8"));
const repository = await writeSampleRepository(sample.files, publicDirectory, sample.id);
await writeFile(manifestPath, `${JSON.stringify({ ...sample, repository })}\n`);
console.log(`Generated static Git snapshot: ${repository.path} (main: ${repository.commit})`);
