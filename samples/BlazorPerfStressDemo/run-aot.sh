#!/usr/bin/env bash
# See ../BlazorDemo/run-aot.sh for the full rationale. This is the
# perf-stress variant — Release+AOT publish, served on :5051 by default
# so it can coexist with BlazorDemo on :5050.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/BlazorPerfStressDemo.csproj"
PORT="${PORT:-5051}"

echo "[run-aot] Publishing $(basename "$PROJECT") (Release + AOT)..."
echo "[run-aot] (first publish takes 1-3 minutes; incremental publishes are faster)"
dotnet publish "$PROJECT" -c Release --nologo

WWWROOT="$SCRIPT_DIR/bin/Release/net10.0/publish/wwwroot"
if [[ ! -d "$WWWROOT" ]]; then
  echo "[run-aot] publish output not found at $WWWROOT" >&2
  exit 1
fi

if command -v lsof >/dev/null 2>&1; then
  stale_pids=$(lsof -nP -t -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null || true)
  if [[ -n "$stale_pids" ]]; then
    echo "[run-aot] Port $PORT is in use by PID(s): $stale_pids — terminating."
    # shellcheck disable=SC2086
    kill $stale_pids 2>/dev/null || true
    sleep 1
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
