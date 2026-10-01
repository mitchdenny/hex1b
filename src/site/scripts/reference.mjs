import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../../../", import.meta.url));
const result = spawnSync("dotnet", ["run", "--project", "src/DocGenerator/Hex1b.DocGenerator.csproj",
  "--", "--output", "src/site/.generated/reference"], { cwd: root, stdio: "inherit" });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error(`API reference generation failed (${result.signal ?? result.status})`);
