#!/usr/bin/env bash
set -euo pipefail

library="${1:?Usage: assert-native-exports.sh <native-library>}"
if [[ "$library" == *.dylib ]]; then
    symbols="$(nm -gU "$library" | awk '{print $NF}' | sed 's/^_//')"
else
    symbols="$(nm -D --defined-only "$library" | awk '{print $NF}')"
fi
for name in \
    hex1b_forkpty_shell hex1b_forkpty_shell_env \
    hex1b_forkpty_exec hex1b_forkpty_exec_env \
    hex1b_forkpty_shell_env_start hex1b_forkpty_exec_env_start \
    hex1b_poll_startup hex1b_abort_startup \
    hex1b_resize hex1b_wait \
    hex1b_termios_size hex1b_termios_get hex1b_termios_make_raw hex1b_termios_set \
    hex1b_get_window_pixel_size; do
    if ! grep -Fxq "$name" <<< "$symbols"; then
        echo "Missing native export: $name in $library" >&2
        exit 1
    fi
done
echo "Verified native startup and compatibility exports: $library"
