# POSIX-shell printf; this tape is not offered for Windows cmd.exe.
Set TypingSpeed 25ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type "printf '\033[31mRed \033[32mGreen \033[34mBlue\033[0m\n'"
Enter
Sleep 500ms
Wait /([#$%>]|[^\x00-\x7f])$/
Sleep 1s
