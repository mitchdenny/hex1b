# POSIX shell. The application writes both halves; they are independent rows.
# Clears the visible screen; leaves the shell on a normal-width row.
Set TypingSpeed 0ms
Set WaitTimeout 10s
Wait /([#$%>]|[^\x00-\x7f])$/
Type `(printf '\033[0m\033[2J\033[H\033#5DEC line rendition spike\r\n\r\nNormal:        Hello Hex1b 123\r\n\033#6Double width: Hello Hex1b\r\n\033#3\033[1;36mHello Hex1b 123\r\n\033#4Hello Hex1b 123\r\n\033#5\033[0m\r\n\033#3\033[4;33mUnicode: caf\303\251 \347\225\214\r\n\033#4Unicode: caf\303\251 \347\225\214\r\n\033#5\033[0m\r\nTop half only (deliberately unpaired):\r\n\033#3INDEPENDENT ROW\r\n\033#5This is a normal row, not a bottom half.\r\n\r\n\033#6\033[?7lClipped without autowrap: 012345678901234567890123456789012345678901234567890123456789\r\n\033#5\033[?7h\r\nResize narrower: enlarged rows truncate, normal rows reflow.\r\nSelect both Hello rows: copying retains both copies.\r\nDEC_DOUBLE_HEIGHT_READY\r\n')`
Enter
Sleep 300ms
Wait /([#$%>]|[^\x00-\x7f])$/
