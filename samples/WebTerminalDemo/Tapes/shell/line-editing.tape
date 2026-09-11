# Demonstrate real shell line editing and history without modifying files.
Set TypingSpeed 60ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type "echo Tape editing worksX"
Sleep 500ms
Backspace
Sleep 500ms
Enter
Sleep 500ms
Wait /([#$%>]|[^\x00-\x7f])$/
Up
Sleep 1s
Enter
Sleep 500ms
Wait /([#$%>]|[^\x00-\x7f])$/
