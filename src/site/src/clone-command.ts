export interface SampleRepository { path: string; directory: string; commit: string }

export function isSampleRepository(value: unknown): value is SampleRepository {
  if (typeof value !== "object" || value === null) return false;
  return "directory" in value && typeof value.directory === "string" &&
    /^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(value.directory) &&
    "commit" in value && typeof value.commit === "string" && /^[a-f0-9]{40}$/u.test(value.commit) &&
    "path" in value && typeof value.path === "string" &&
    new RegExp(`^repositories/${value.directory}/[a-f0-9]{64}\\.git$`, "u").test(value.path);
}

export function cloneCommand(base: string, repository: SampleRepository): string {
  if (!isSampleRepository(repository)) throw new Error("Invalid sample repository metadata");
  const url = new URL(repository.path, base);
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password)
    throw new Error("Sample cloning requires an HTTP(S) site URL without credentials");
  // Single-quoted arguments work in both Bash and PowerShell; encode embedded apostrophes.
  return `git clone '${url.href.replaceAll("'", "%27")}' ${repository.directory}`;
}
