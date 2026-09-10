# Start at an idle shell prompt. Do not clear or replace the existing terminal.
Set TypingSpeed 50ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type "echo Hello from Tape"
Enter
Sleep 500ms
Wait /([#$%>]|[^\x00-\x7f])$/
Sleep 1s
