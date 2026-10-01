# POSIX-shell printf; this tape is not offered for Windows cmd.exe.
Set TypingSpeed 25ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/

# Report a working directory (OSC 7), then mark a fresh prompt (OSC 133;A).
# Repeat the upcoming command's percent-encoded detail on A/B/C so each mark
# can be inspected independently. These are the actual demo commands, not npm.
# D accepts only the exit status; it cannot carry command detail.
Type "printf '\033]7;file://localhost/home/demo/project\007\033]133;A;cmdline_url=%s\007' 'echo%20%27Building%20project...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Build%20succeeded.%27'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Command 1: a simulated successful build. B marks input, C marks execution,
# and D reports the real exit code. Use %s so printf does not interpret the URLs.
# Give D its own output row so the next shell prompt redraw cannot erase it.
Type "printf '\033]133;B;cmdline_url=%s\007' 'echo%20%27Building%20project...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Build%20succeeded.%27'; printf '\033]133;C;cmdline_url=%s\007' 'echo%20%27Building%20project...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Build%20succeeded.%27'; echo 'Building project...' && sleep 0.3 && echo 'Build succeeded.'; printf '\033]133;D;%s\007Build command finished.\n' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Command 2: a failing command, so D reports a non-zero exit code.
Type "printf '\033]133;A;cmdline_url=%s\007' 'ls%20%2Fno%2Fsuch%2Fpath'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
Type "printf '\033]133;B;cmdline_url=%s\007' 'ls%20%2Fno%2Fsuch%2Fpath'; printf '\033]133;C;cmdline_url=%s\007' 'ls%20%2Fno%2Fsuch%2Fpath'; ls /no/such/path; printf '\033]133;D;%s\007Missing-path command finished.\n' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Move into a different directory (OSC 7 changes again) for a final command.
Type "printf '\033]7;file://localhost/home/demo/project/dist\007\033]133;A;cmdline_url=%s\007' 'echo%20%27Deploying%20dist%2F...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Deploy%20complete.%27'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
Type "printf '\033]133;B;cmdline_url=%s\007' 'echo%20%27Deploying%20dist%2F...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Deploy%20complete.%27'; printf '\033]133;C;cmdline_url=%s\007' 'echo%20%27Deploying%20dist%2F...%27%20%26%26%20sleep%200.3%20%26%26%20echo%20%27Deploy%20complete.%27'; echo 'Deploying dist/...' && sleep 0.3 && echo 'Deploy complete.'; printf '\033]133;D;%s\007Deploy command finished.\n' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Leave enough history for the scrollbar to appear in a typical demo window.
# This unmarked display helper is not one of the three command examples.
Type `i=1; while [ "$i" -le 40 ]; do printf 'Inspection history row %02d\n' "$i"; i=$((i+1)); done; printf 'Hover command ticks in the scrollbar to inspect command text.\nFinished ticks show exit status only.\n'`
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
Sleep 1s
