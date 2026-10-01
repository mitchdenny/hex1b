import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { devNull, tmpdir } from "node:os";
import { join, resolve, sep } from "node:path";
import { promisify } from "node:util";
import { test } from "node:test";
import { writeSampleRepository } from "../scripts/sample-repository.mjs";
import { cloneCommand, isSampleRepository } from "../src/clone-command.ts";

const execute = promisify(execFile);
const file = (path, content) => ({ path, content, language: "plaintext" });
const files = [
  file("Program.cs", "Console.WriteLine(\"hello\");\n"),
  file("Art/Scene.cs", "// preserved CRLF\r\n"),
  file("Art/Space and café.txt", "Unicode contents: café\n"),
  file(".gitignore", "bin/\nobj/\n"),
];
const git = async (cwd, ...args) => (await execute("git", ["-c", "core.autocrlf=false", ...args], {
  cwd, timeout: 20_000,
  env: {
    ...Object.fromEntries(Object.entries(process.env)
      .filter(([key]) => !key.startsWith("GIT_") || key === "GIT_EXEC_PATH")),
    GIT_CONFIG_NOSYSTEM: "1", GIT_CONFIG_GLOBAL: devNull, GIT_TERMINAL_PROMPT: "0",
  },
})).stdout.trim();

test("Static repositories clone at root and project prefixes using only HTTP GETs", { timeout: 60_000 }, async t => {
  const root = await mkdtemp(join(tmpdir(), "hex1b-clone-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const publicDirectory = join(root, "public");
  const repository = await writeSampleRepository(files, publicDirectory, "demo");
  const requests = [];
  const server = createServer(async (request, response) => {
    requests.push({ method: request.method, url: request.url });
    if (request.method !== "GET") { response.writeHead(405).end(); return; }
    try {
      const url = new URL(request.url, "http://localhost");
      const path = decodeURIComponent(url.pathname.replace(/^\/hex1b\//u, "/"));
      const asset = resolve(publicDirectory, `.${path}`);
      if (!asset.startsWith(`${publicDirectory}${sep}`)) { response.writeHead(404).end(); return; }
      const content = await readFile(asset);
      response.writeHead(200, { "Content-Type": "application/octet-stream" }).end(content);
    } catch (error) {
      if (error.code === "ENOENT" || error.code === "EISDIR") response.writeHead(404).end();
      else { console.error(error); response.writeHead(500).end(); }
    }
  });
  t.after(() => new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve())));
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  for (const prefix of ["", "hex1b/"]) {
    const destination = join(root, prefix ? "project-clone" : "root-clone");
    const base = `http://127.0.0.1:${server.address().port}/${prefix}`;
    await git(root, "clone", `${base}${repository.path}`, destination);
    assert.equal(await git(destination, "rev-parse", "HEAD"), repository.commit);
    assert.equal(await git(destination, "branch", "--show-current"), "main");
    assert.equal(await git(destination, "rev-list", "--count", "HEAD"), "1");
    await git(destination, "fsck", "--strict");
    assert.equal(await git(destination, "status", "--porcelain"), "");
    for (const source of files)
      assert.deepEqual(await readFile(join(destination, source.path)), Buffer.from(source.content));
    const tracked = await git(destination, "-c", "core.quotePath=false", "ls-files");
    assert.deepEqual(tracked.split("\n").sort(), files.map(source => source.path).sort());
  }
  assert.ok(requests.some(request => request.url.includes("info/refs?service=git-upload-pack")));
  assert.ok(requests.some(request => request.url.endsWith(".pack")));
  assert.ok(requests.every(request => request.method === "GET"));
  const assets = await readdir(join(publicDirectory, repository.path), { recursive: true });
  assert.ok(!assets.some(path => /^(config|hooks|logs|index|description|packed-refs)([/\\]|$)/u.test(path)));
  assert.ok(assets.includes("HEAD"));
});

test("Snapshots are deterministic, content-addressed and independent of local Git configuration", async t => {
  const root = await mkdtemp(join(tmpdir(), "hex1b-repro-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const first = await writeSampleRepository(files, root, "demo");
  const config = join(root, "host.gitconfig");
  await writeFile(config, "[user]\n\tname = Should not leak\n\temail = local@example.invalid\n[commit]\n\tgpgSign = true\n");
  const original = process.env.GIT_CONFIG_GLOBAL;
  process.env.GIT_CONFIG_GLOBAL = config;
  try {
    assert.deepEqual(await writeSampleRepository([...files].reverse(), root, "demo"), first);
  } finally {
    if (original === undefined) delete process.env.GIT_CONFIG_GLOBAL;
    else process.env.GIT_CONFIG_GLOBAL = original;
  }
  const changed = await writeSampleRepository([...files, file("README.md", "New file")], root, "demo");
  assert.notEqual(changed.path, first.path);
  assert.notEqual(changed.commit, first.commit);
  assert.equal(await readFile(join(root, first.path, "refs/heads/main"), "utf8"), `${first.commit}\n`);
  assert.match(await git(root, "--git-dir", join(root, first.path), "cat-file", "-p", first.commit),
    /author Hex1b sample build <samples@hex1b.invalid>/u);
});

test("Repository export rejects traversal, conflicting paths and Git metadata", async () => {
  for (const path of ["../outside.cs", "/outside.cs", ".git/config", "nested/.GIT/HEAD"]) {
    await assert.rejects(writeSampleRepository([file(path, "invalid")], ".", "demo"), /Invalid|Reserved/u);
  }
  await assert.rejects(writeSampleRepository(files, ".", "../demo"), /Invalid/u);
  await assert.rejects(writeSampleRepository([file("a", ""), file("a/b", "")], ".", "demo"), /directory/u);
});

test("Clone commands preserve HTTP(S) deployment prefixes and quote shell arguments", () => {
  const repository = {
    path: `repositories/demo/${"a".repeat(64)}.git`, directory: "demo", commit: "b".repeat(40),
  };
  for (const base of ["https://hex1b.dev/", "https://example.github.io/hex1b/", "http://localhost:4174/hex1b/"]) {
    assert.equal(cloneCommand(base, repository), `git clone '${base}${repository.path}' demo`);
  }
  assert.equal(cloneCommand("https://example.org/it's $safe/", repository),
    `git clone 'https://example.org/it%27s%20$safe/${repository.path}' demo`);
  assert.equal(isSampleRepository({ ...repository, path: "https://other.example/repo.git" }), false);
  assert.equal(isSampleRepository({ ...repository, directory: "demo;evil" }), false);
  assert.throws(() => cloneCommand("file:///tmp/", repository), /HTTP/u);
  assert.throws(() => cloneCommand("https://user:password@example.org/", repository), /credentials/u);
});
