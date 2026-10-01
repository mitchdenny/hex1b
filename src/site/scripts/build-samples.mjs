import { execFile, spawnSync } from "node:child_process";
import { cp, mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";
import { writeSampleRepository } from "./sample-repository.mjs";
import { parseRecording } from "../../web-terminal/.build/recording.js";
import { decodeFrame } from "../../web-terminal/.build/protocol.js";

const execute = promisify(execFile);
const site = fileURLToPath(new URL("../", import.meta.url));
const root = resolve(site, "../..");
const generated = join(site, ".generated");
const output = join(site, "public/recordings");
await mkdir(generated, { recursive: true });
const allSamples = JSON.parse(await readFile(join(site, "samples/catalog.json"), "utf8"));
if (!Array.isArray(allSamples) || !allSamples.length) throw new Error("No standalone samples in the catalog");
const requested = new Set(process.argv.slice(2));
for (const id of requested)
  if (id !== "pixel-postcards" && !allSamples.some(sample => sample.id === id))
    throw new Error(`Unknown requested sample: ${id}`);
const catalog = requested.size ? allSamples.filter(sample => requested.has(sample.id)) : allSamples;
const previous = requested.size
  ? JSON.parse(await readFile(join(generated, "samples.json"), "utf8"))
  : [];
if (requested.size && allSamples.some(sample => !requested.has(sample.id) && !previous.some(old => old.id === sample.id)))
  throw new Error("Run the complete samples build before rebuilding individual samples");
const ids = new Set();
for (const sample of catalog) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(sample.id) || ids.has(sample.id))
    throw new Error(`Invalid or duplicate sample ID: ${sample.id}`);
  if (sample.sourceOnly && (typeof sample.prerequisites !== "string" || !sample.prerequisites.trim()))
    throw new Error(`Source-only sample lacks documented prerequisites: ${sample.id}`);
  ids.add(sample.id);
}
let next = 0;
const failures = [];
await Promise.all(Array.from({ length: 2 }, async () => {
  while (next < catalog.length) {
    const sample = catalog[next++];
    const project = resolve(site, sample.project);
    if (!project.startsWith(`${join(site, "samples")}/`)) throw new Error(`Invalid sample project: ${sample.project}`);
    try {
      await execute("dotnet", ["build", project, "-v", "q", "-m:1",
        "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"],
      { cwd: site, timeout: 120_000, maxBuffer: 4 * 1024 * 1024 });
      console.log(`Built ${sample.id}`);
    } catch (error) {
      failures.push(sample.id);
      console.error(`Build failed for ${sample.id}\n${error.stdout ?? ""}\n${error.stderr ?? error.message}`);
    }
  }
}));
if (failures.length) throw new Error(`Standalone sample builds failed: ${failures.join(", ")}`);
const prepared = catalog.filter(sample => !sample.sourceOnly).map(sample => ({
  ...sample,
  dll: join(dirname(resolve(site, sample.project)), "bin/Debug/net10.0/Sample.dll"),
  columns: sample.columns ?? 100, rows: sample.rows ?? 30,
}));
await writeFile(join(generated, "recording-catalog.json"), JSON.stringify(prepared));
const run = (command, args) => {
  const result = spawnSync(command, args, { cwd: root, stdio: "inherit" });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} failed (${result.signal ?? result.status})`);
};
if (prepared.length) run("dotnet", ["run", "--project", "src/site/driver/SiteRecordingDriver.csproj",
  "--", join(generated, "recording-catalog.json"), output]);

async function sourceFiles(directory, prefix = "") {
  const result = [];
  for (const item of (await readdir(directory, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name))) {
    if (["bin", "obj", ".git"].includes(item.name)) continue;
    const path = `${prefix}${item.name}`;
    if (item.isDirectory()) result.push(...await sourceFiles(join(directory, item.name), `${path}/`));
    else if (item.isFile()) {
      const content = new TextDecoder("utf-8", { fatal: true }).decode(await readFile(join(directory, item.name)));
      result.push({ path, content, language: path.endsWith(".cs") ? "csharp" : path.endsWith(".csproj") ? "xml" : "plaintext" });
    } else throw new Error(`Unsupported sample file: ${path}`);
  }
  return result;
}
const manifests = previous.filter(sample => !requested.has(sample.id));
for (const sample of catalog) {
  if (!sample.sourceOnly) {
    const recording = parseRecording(await readFile(join(output, sample.id, "recording.hwt.json"), "utf8"));
    const frame = decodeFrame(recording.frames[0].data);
    if (frame.metadata.columns !== (sample.columns ?? 100) || frame.metadata.rows !== (sample.rows ?? 30))
      throw new Error(`Recording dimensions do not match the sample catalog: ${sample.id}`);
  }
  const files = await sourceFiles(dirname(resolve(site, sample.project)));
  const repository = await writeSampleRepository(files, join(site, "public"), sample.id);
  const manifest = {
    ...sample, description: sample.description ?? (sample.sourceOnly ? `Standalone source for ${sample.title}.` :
      `Standalone ${sample.title} example. This initial recording captures its starting state; scenarios are refined as the documentation is revised.`),
    command: "dotnet run", recording: sample.sourceOnly ? undefined : "recording.hwt.json", files, repository,
  };
  await mkdir(join(output, sample.id), { recursive: true });
  await writeFile(join(output, sample.id, "sample.json"), JSON.stringify(manifest));
  manifests.push(manifest);
}

if (!requested.size || requested.has("pixel-postcards")) {
  run("node", ["src/site/scripts/record.mjs"]);
  run("node", ["--experimental-strip-types", "src/site/scripts/generate-repositories.mjs"]);
  const postcard = JSON.parse(await readFile(join(output, "sample.json"), "utf8"));
  await mkdir(join(output, postcard.id), { recursive: true });
  await cp(join(output, postcard.recording), join(output, postcard.id, postcard.recording));
  await writeFile(join(output, postcard.id, "sample.json"), JSON.stringify(postcard));
  manifests.push(postcard);
}
await writeFile(join(generated, "samples.json"), JSON.stringify(manifests));
console.log(`Prepared ${manifests.length} cloneable samples (${manifests.filter(sample => !sample.sourceOnly).length} recorded).`);
