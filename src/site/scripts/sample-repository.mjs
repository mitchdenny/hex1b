import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { cp, mkdir, mkdtemp, readFile, readdir, rm } from "node:fs/promises";
import { devNull, tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { createFileTree, sourceFiles } from "../src/sample-files.ts";

export async function writeSampleRepository(files, publicDirectory, slug) {
  if (typeof slug !== "string" || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(slug))
    throw new Error("Invalid sample repository slug");
  const tree = createFileTree(files);
  for (const { path, file } of sourceFiles(tree)) {
    if (path.split("/").some(part => part.toLowerCase() === ".git"))
      throw new Error(`Reserved Git path in sample: ${path}`);
    if (typeof file.content !== "string") throw new Error(`Missing sample file content: ${path}`);
  }

  const staging = await mkdtemp(join(tmpdir(), "hex1b-sample-repository-"));
  try {
    const repository = join(staging, "repository");
    const template = join(staging, "empty-template");
    await mkdir(template);
    // Do not inherit signing, hooks, attributes, object stores or Git configuration.
    const env = {
      ...Object.fromEntries(Object.entries(process.env)
        .filter(([key]) => !key.startsWith("GIT_") || key === "GIT_EXEC_PATH")),
      GIT_CONFIG_NOSYSTEM: "1",
      GIT_CONFIG_GLOBAL: devNull,
      GIT_AUTHOR_NAME: "Hex1b sample build",
      GIT_AUTHOR_EMAIL: "samples@hex1b.invalid",
      GIT_COMMITTER_NAME: "Hex1b sample build",
      GIT_COMMITTER_EMAIL: "samples@hex1b.invalid",
      GIT_AUTHOR_DATE: "2000-01-01T00:00:00Z",
      GIT_COMMITTER_DATE: "2000-01-01T00:00:00Z",
    };
    const git = (args, input) => {
      const result = spawnSync("git", ["--git-dir", repository, ...args], {
        cwd: staging, env, input, encoding: "utf8", maxBuffer: 16 * 1024 * 1024,
      });
      if (result.error) throw result.error;
      if (result.status !== 0)
        throw new Error(`Sample repository git ${args[0]} failed (${result.signal ?? result.status}): ${result.stderr}`);
      return result.stdout.trim();
    };
    git(["init", "--bare", "--initial-branch=main", "--object-format=sha1", `--template=${template}`]);
    const writeTree = directory => git(["mktree", "-z"], directory.children.map(entry => {
      const hash = entry.kind === "directory"
        ? writeTree(entry)
        : git(["hash-object", "-w", "--stdin"], entry.file.content);
      return `${entry.kind === "directory" ? "040000 tree" : "100644 blob"} ${hash}\t${entry.name}\0`;
    }).join(""));
    const commit = git(["commit-tree", writeTree(tree)], "Sample snapshot\n");
    git(["update-ref", "refs/heads/main", commit]);
    git(["repack", "-ad"]);
    git(["update-server-info"]);
    git(["fsck", "--strict"]);

    const packs = (await readdir(join(repository, "objects/pack")))
      .filter(name => /^pack-[a-f0-9]{40}\.(pack|idx)$/u.test(name));
    const paths = ["HEAD", "refs/heads/main", "info/refs", "objects/info/packs",
      ...packs.map(name => `objects/pack/${name}`)].sort();
    const digest = createHash("sha256");
    for (const path of paths) {
      const bytes = await readFile(join(repository, path));
      digest.update(path).update("\0").update(String(bytes.length)).update("\0").update(bytes);
    }
    // Include the actual pack bytes in the URL so Git/version changes cannot poison caches.
    const path = `repositories/${slug}/${digest.digest("hex")}.git`;
    for (const asset of paths) {
      const destination = join(publicDirectory, path, asset);
      await mkdir(dirname(destination), { recursive: true });
      await cp(join(repository, asset), destination);
    }
    return { path, directory: slug, commit };
  } finally {
    await rm(staging, { recursive: true, force: true });
  }
}
