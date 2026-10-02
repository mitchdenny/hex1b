# End the real shell; unlike End terminal, this reports a process exit code.
Set TypingSpeed 10ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type "echo FINAL-OUTPUT-EXIT-0"
Enter
Wait /([#$%>]|[^\x00-\x7f])$/
Type "exit 0"
Enter
