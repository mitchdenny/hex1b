export interface SampleFile { path: string; language: string; content: string }
export interface SourceFile { kind: "file"; name: string; path: string; file: SampleFile }
export interface SourceDirectory { kind: "directory"; name: string; path: string; children: SourceEntry[] }
export type SourceEntry = SourceDirectory | SourceFile;

/** Build a read-only virtual tree without changing the displayed source contents. */
export function createFileTree(files: readonly SampleFile[]): SourceDirectory {
  if (!files.length) throw new Error("The sample contains no source files");
  const root: SourceDirectory = { kind: "directory", name: "Sample", path: "", children: [] };
  for (const file of files) {
    const path = file.path.replaceAll("\\", "/");
    const parts = path.split("/");
    if (/[\u0000-\u001f\u007f:]/u.test(path) || parts.some(part => !part || part === "." || part === ".."))
      throw new Error(`Invalid sample file path: ${file.path}`);
    let parent = root;
    for (const [index, name] of parts.entries()) {
      const existing = parent.children.find(entry => entry.name === name);
      if (index === parts.length - 1) {
        if (existing) throw new Error(`Duplicate or conflicting sample file path: ${path}`);
        parent.children.push({ kind: "file", name, path, file: { ...file, path } });
      } else {
        if (existing?.kind === "file") throw new Error(`File used as a sample directory: ${existing.path}`);
        if (existing) parent = existing;
        else {
          const directory: SourceDirectory = {
            kind: "directory", name, path: parts.slice(0, index + 1).join("/"), children: [],
          };
          parent.children.push(directory);
          parent = directory;
        }
      }
    }
  }
  const sort = (directory: SourceDirectory): void => {
    directory.children.sort((a, b) => {
      if (a.kind !== b.kind) return a.kind === "directory" ? -1 : 1;
      return a.name.localeCompare(b.name, "en");
    });
    for (const child of directory.children) if (child.kind === "directory") sort(child);
  };
  sort(root);
  const project = root.children.find(entry => entry.kind === "file" && entry.name.endsWith(".csproj"));
  if (project) root.name = project.name.slice(0, -".csproj".length);
  return root;
}

export function sourceFiles(directory: SourceDirectory): SourceFile[] {
  return directory.children.flatMap(entry => entry.kind === "file" ? [entry] : sourceFiles(entry));
}
