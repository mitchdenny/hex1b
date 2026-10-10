#!/usr/bin/env bash
set -euo pipefail

library="${1:?Usage: assert-musl-native.sh <native-library>}"
readelf -h "$library" | grep -Eq 'Machine:.*Advanced Micro Devices X86-64'
dependencies="$(readelf -d "$library" | grep '(NEEDED)')"
symbols="$(readelf --version-info --dyn-syms "$library")"
if ! grep -Fq '[libc.musl-x86_64.so.1]' <<< "$dependencies"; then
    echo "Missing x64 musl libc dependency: $library" >&2
    exit 1
fi
if grep -Eq '\[(libc\.so\.6|libpthread\.so\.0|libutil\.so\.1)\]' <<< "$dependencies" ||
    grep -q 'GLIBC_' <<< "$symbols"; then
    echo "Found glibc dependencies or versioned symbols: $library" >&2
    exit 1
fi
echo "Verified x64 musl ELF dependencies and absence of GLIBC symbols: $library"
