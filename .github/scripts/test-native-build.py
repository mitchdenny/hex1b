#!/usr/bin/env python3
"""Exercise native interop through sample builds, runs, publish, and packaging."""

import errno
import fcntl
import os
from pathlib import Path
import platform
import pty
import select
import signal
import struct
import subprocess
import termios
import time
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[2]
SAMPLE = ROOT / "samples/KgpCloudDemo"
SYSTEM = {"Linux": "linux", "Darwin": "osx"}[platform.system()]
ARCH = {"aarch64": "arm64", "arm64": "arm64", "x86_64": "x64"}[platform.machine()]
RID = f"{SYSTEM}-{ARCH}"
LIBRARY = "libhex1binterop.dylib" if SYSTEM == "osx" else "libhex1binterop.so"
NATIVE = ROOT / "src/Hex1b/runtimes" / RID / "native" / LIBRARY


def run_sample(*options):
    # Do not use --headless: that bypasses the native console driver.
    command = ["dotnet", "run", *options, "--", "--frames", "2", "--motes", "4"]
    print(f"Running {' '.join(command)}", flush=True)
    master, slave = pty.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 24, 80, 0, 0))
    output = bytearray()
    process = None
    try:
        process = subprocess.Popen(
            command,
            cwd=SAMPLE,
            stdin=slave,
            stdout=slave,
            stderr=slave,
            start_new_session=True,
            env={**os.environ, "TERM": "xterm-256color", "MSBUILDTERMINALLOGGER": "off"},
        )
        os.close(slave)
        slave = None
        deadline = time.monotonic() + 180
        while True:
            if time.monotonic() >= deadline:
                raise TimeoutError("Sample did not finish within 180 seconds")
            if select.select([master], [], [], 0.1)[0]:
                try:
                    data = os.read(master, 65536)
                except OSError as error:
                    if error.errno != errno.EIO:
                        raise
                    break
                if not data:
                    break
                output.extend(data)
            elif process.poll() is not None:
                break
        if process.wait(timeout=10) != 0:
            raise AssertionError(f"Sample exited with {process.returncode}")
        if b"\x1b_G" not in output:
            raise AssertionError("Sample did not render kitty graphics through the console")
    except BaseException:
        print(output.decode("utf-8", errors="replace"))
        raise
    finally:
        if process is not None and process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
        os.close(master)
        if slave is not None:
            os.close(slave)


def assert_native_copy(directory):
    copied = directory / LIBRARY
    if copied.read_bytes() != NATIVE.read_bytes():
        raise AssertionError(f"{copied} does not match {NATIVE}")


# On a clean ARM64 checkout, the tracked x64 library also exists. Neither it nor
# a stale consumer output must win over the freshly generated host library.
run_sample()
output = SAMPLE / "bin/Debug/net10.0"
assert_native_copy(output)
native_mtime = NATIVE.stat().st_mtime_ns
(output / LIBRARY).write_bytes(b"stale native output")
run_sample()
assert_native_copy(output)
if NATIVE.stat().st_mtime_ns != native_mtime:
    raise AssertionError("An unchanged native library was unnecessarily rebuilt")

# A native compiler failure must stop dotnet build, even with an older .so or
# .dylib still present. The following normal run must rebuild the missing object.
native_object = ROOT / "src/Hex1b/obj/native" / RID / "hex1binterop.o"
native_object.unlink()
failed_build = subprocess.run(
    ["dotnet", "build", "--no-restore", "--verbosity", "quiet"],
    cwd=SAMPLE,
    env={**os.environ, "MAKEFLAGS": "CC=false"},
    stdout=subprocess.PIPE,
    stderr=subprocess.STDOUT,
    text=True,
)
if failed_build.returncode == 0 or "MSB3073" not in failed_build.stdout:
    raise AssertionError(f"Native compiler failure was not propagated:\n{failed_build.stdout}")
run_sample()
assert_native_copy(output)
if NATIVE.stat().st_mtime_ns == native_mtime:
    raise AssertionError("Missing native object did not trigger recompilation")

run_sample("--runtime", RID)
assert_native_copy(output / RID)

subprocess.run(
    ["dotnet", "publish", "--runtime", RID, "--self-contained", "false",
     "--configuration", "Debug", "--verbosity", "quiet"],
    cwd=SAMPLE,
    check=True,
)
assert_native_copy(output / RID / "publish")

if SYSTEM == "osx":
    other_rid = "osx-x64" if ARCH == "arm64" else "osx-arm64"
    subprocess.run(
        ["dotnet", "build", "--runtime", other_rid, "--verbosity", "quiet"],
        cwd=SAMPLE,
        check=True,
    )
    other_native = NATIVE.parents[2] / other_rid / "native" / LIBRARY
    if (output / other_rid / LIBRARY).read_bytes() != other_native.read_bytes():
        raise AssertionError("Cross-runtime build copied the host library")
    run_sample()
    assert_native_copy(output)

package_output = ROOT / "src/Hex1b/bin/Debug/native-build-test"
subprocess.run(
    ["dotnet", "pack", str(ROOT / "src/Hex1b/Hex1b.csproj"),
     "--configuration", "Debug", "--output", str(package_output),
     "-p:PackageVersion=1.0.0-native-build-test", "--verbosity", "quiet"],
    cwd=ROOT,
    check=True,
)
with ZipFile(package_output / "Hex1b.1.0.0-native-build-test.nupkg") as package:
    native_assets = [name for name in package.namelist() if "libhex1binterop." in name]
    if len(native_assets) != len(set(native_assets)):
        raise AssertionError("Native library appears more than once in the package")
    if any(not name.startswith("runtimes/") for name in native_assets):
        raise AssertionError("Native libraries must be packaged only as runtime assets")
    for native in (ROOT / "src/Hex1b/runtimes").glob("*/native/libhex1binterop.*"):
        relative = native.relative_to(ROOT / "src/Hex1b").as_posix()
        if package.read(relative) != native.read_bytes():
            raise AssertionError(f"Package does not contain the correct {relative}")

print(f"Native build/run/publish/package checks passed for {RID}.")
