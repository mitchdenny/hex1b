#!/usr/bin/env bash
# Publish the BlazorDemo with Mono AOT and serve the static output locally.
#
# Why this script exists:
#   `dotnet run` on a Blazor WASM project always uses the interpreter — the
#   dev server skips the AOT toolchain because AOT publishing takes minutes.
#   For interactive perf testing of the WASM hosting story we want the AOT
#   build, which is dramatically faster on hot numeric loops (the globe paint
#   loop in particular goes from ~3 fps to "completely usable").
#
# Usage:
#   ./samples/BlazorDemo/run-aot.sh           # publish + serve on :5050
#   PORT=8080 ./samples/BlazorDemo/run-aot.sh # pick a different port
#
# Requires: `wasm-tools` workload (dotnet workload install wasm-tools).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/BlazorDemo.csproj"
PORT="${PORT:-5050}"

echo "[run-aot] Publishing $(basename "$PROJECT") (Release + AOT)..."
echo "[run-aot] (first publish takes 1-3 minutes; incremental publishes are faster)"
dotnet publish "$PROJECT" -c Release --nologo

WWWROOT="$SCRIPT_DIR/bin/Release/net10.0/publish/wwwroot"
if [[ ! -d "$WWWROOT" ]]; then
  echo "[run-aot] publish output not found at $WWWROOT" >&2
  exit 1
fi

# If a previous run-aot.sh (or anything else) is already bound to PORT,
# stop it so this run can take over. Without this you'd see a confusing
# "Address already in use" after re-running the script.
if command -v lsof >/dev/null 2>&1; then
  stale_pids=$(lsof -nP -t -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null || true)
  if [[ -n "$stale_pids" ]]; then
    echo "[run-aot] Port $PORT is in use by PID(s): $stale_pids — terminating."
    # shellcheck disable=SC2086
    kill $stale_pids 2>/dev/null || true
    sleep 1
    # Force-kill any survivors.
    survivors=$(lsof -nP -t -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null || true)
    if [[ -n "$survivors" ]]; then
      # shellcheck disable=SC2086
      kill -9 $survivors 2>/dev/null || true
      sleep 1
    fi
  fi
fi

echo "[run-aot] Serving $WWWROOT on http://localhost:$PORT ..."
echo "[run-aot] (Ctrl+C to stop; hard-reload Cmd+Shift+R after each republish)"
cd "$WWWROOT"
exec python3 -m http.server "$PORT"
