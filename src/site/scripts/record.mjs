import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../../../", import.meta.url));
const result = spawnSync("dotnet", [
  "run", "--project", "samples/HwtRecordingDriver/HwtRecordingDriver.csproj",
  "--", "--output", "src/site/public/recordings",
], { cwd: root, stdio: "inherit" });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error(`Recording driver failed (${result.signal ?? result.status})`);
