#!/usr/bin/env python3
"""POSIX PTY startup checks for built samples; no actions, recordings, or exports."""
import concurrent.futures
import errno
import fcntl
import json
import os
import pathlib
import pty
import re
import select
import signal
import struct
import subprocess
import sys
import termios
import time

ROOT = pathlib.Path(__file__).resolve().parent
OUTPUT = ROOT / ".validation"
VERIFY_READY = "--verify-ready" in sys.argv[1:]


def plain_output(output):
    text = output.decode("utf-8", errors="replace")
    text = re.sub(r"\x1b\][\s\S]*?(?:\x07|\x1b\\)", "", text)
    text = re.sub(r"\x1b[P_][\s\S]*?\x1b\\", "", text)
    return re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", text)


def smoke(entry):
    sample_id = entry["id"]
    if entry.get("sourceOnly"):
        return {"id": sample_id, "success": True, "skipped": True, "reason": "Source-only samples must never be auto-run."}
    directory = OUTPUT / sample_id
    assembly = directory / "bin/Debug/net10.0/Sample.dll"
    if not assembly.exists():
        return {"id": sample_id, "success": False, "reason": "Run validate.py first."}
    master, slave = pty.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", entry["rows"], entry["columns"], 0, 0))
    env = dict(os.environ, TERM="xterm-256color", COLORTERM="truecolor")
    process = subprocess.Popen(
        ["dotnet", str(assembly)], cwd=directory, env=env,
        stdin=slave, stdout=slave, stderr=slave, start_new_session=True,
    )
    os.close(slave)
    chunks = []
    try:
        deadline = time.monotonic() + (5 if VERIFY_READY else 2)
        ready_deadline = None
        while time.monotonic() < deadline:
            if select.select([master], [], [], 0.1)[0]:
                try:
                    chunk = os.read(master, 65536)
                except OSError as error:
                    if error.errno == errno.EIO:
                        break
                    raise
                if not chunk:
                    break
                chunks.append(chunk)
                if b"\x1b[6n" in chunk:
                    os.write(master, b"\x1b[1;1R")
                if b"\x1b[c" in chunk or b"\x1b[0c" in chunk:
                    os.write(master, b"\x1b[?1;2c")
            elif process.poll() is not None:
                break
            if VERIFY_READY and entry.get("readyText") and ready_deadline is None:
                if entry["readyText"] in plain_output(b"".join(chunks)):
                    ready_deadline = time.monotonic() + 0.2
            if ready_deadline is not None and time.monotonic() >= ready_deadline:
                break
        output = b"".join(chunks)
        success = bool(output) and b"Unhandled exception" not in output and process.poll() in (None, 0)
        if VERIFY_READY and entry.get("readyText"):
            success = success and entry["readyText"] in plain_output(output)
        (OUTPUT / f"{sample_id}.startup.log").write_bytes(output)
        result = {"id": sample_id, "success": success, "outputBytes": len(output), "exitCode": process.poll()}
        print(f"{'PASS' if success else 'FAIL'} {sample_id} ({len(output)} bytes)", flush=True)
        return result
    finally:
        if process.poll() is None:
            process.send_signal(signal.SIGINT)
            try:
                process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
        os.close(master)


def main():
    selected = set(sys.argv[1:]) - {"--verify-ready"}
    catalog = json.loads((ROOT / "catalog.json").read_text())
    candidates = [entry for entry in catalog if not selected or entry["id"] in selected]
    entries = [entry for entry in candidates if not entry.get("sourceOnly")
               and (not VERIFY_READY or entry.get("readyText"))]
    skipped = sum(bool(entry.get("sourceOnly")) for entry in candidates)
    if skipped:
        print(f"Skipped {skipped} source-only samples; these are never auto-run.")
    OUTPUT.mkdir(exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        results = list(executor.map(smoke, entries))
    (OUTPUT / "startup-results.json").write_text(json.dumps(results, indent=2) + "\n")
    failed = sum(not result["success"] for result in results)
    print(f"Started {len(results)} samples; {failed} failures.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
