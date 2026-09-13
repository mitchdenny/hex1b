# POSIX-shell printf; this tape is not offered for Windows cmd.exe.
Set TypingSpeed 25ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/

# Report a working directory (OSC 7), then mark a fresh prompt (OSC 133;A).
Type "printf '\033]7;file://localhost/home/demo/project\007\033]133;A\007'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Command 1: a successful build. B marks the start of input, C marks execution
# (with a cmdline_url extension), and D reports the real exit code.
Type "printf '\033]133;B\007'; printf '\033]133;C;cmdline_url=%s\007' 'npm%20run%20build'; echo 'Building project...' && sleep 0.3 && echo 'Build succeeded.'; printf '\033]133;D;%s\007' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Command 2: a failing command, so D reports a non-zero exit code.
Type "printf '\033]133;A\007'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
Type "printf '\033]133;B\007'; printf '\033]133;C\007'; ls /no/such/path; printf '\033]133;D;%s\007' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/

# Move into a different directory (OSC 7 changes again) for a final command.
Type "printf '\033]7;file://localhost/home/demo/project/dist\007\033]133;A\007'"
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
Type "printf '\033]133;B\007'; printf '\033]133;C\007'; echo 'Deploying dist/...' && sleep 0.3 && echo 'Deploy complete.'; printf '\033]133;D;%s\007' $?"
Enter
Sleep 800ms
Wait /([#$%>]|[^\x00-\x7f])$/
Sleep 1s
