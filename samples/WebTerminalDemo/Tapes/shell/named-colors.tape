# POSIX shell. Uses ANSI slots, not RGB, so existing output follows palette changes.
# Run at an idle prompt; the subshell keeps loop variables out of the user's shell.
Set TypingSpeed 0ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type `(printf '\033[0m\nNamed ANSI colors - background swatches and foreground text\n\n'; i=0; for name in Black Red Green Yellow Blue Magenta Cyan White; do printf '%-7s [\033[%sm          \033[0m] \033[%smSample\033[0m   Bright %-7s [\033[%sm          \033[0m] \033[%smSample\033[0m\n' "$name" "$((40+i))" "$((30+i))" "$name" "$((100+i))" "$((90+i))"; i=$((i+1)); done; printf '\nDefault foreground on default background.\nSwitch light/dark mode or palettes to recolor these same swatches.\n')`
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
