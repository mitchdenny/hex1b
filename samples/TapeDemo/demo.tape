# The same script can drive an ordinary interactive shell on Unix or Windows.
Set TypingSpeed 25ms
Set WaitTimeout 5s
Wait />$/
Type "echo Tape playback complete"
Enter
Wait+Screen /\nTape playback complete\n/
Sleep 100ms
