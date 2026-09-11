# TapeDemo runs this source tape from its directory, alongside ../AsciiEarth.
Set TypingSpeed 25ms
Set WaitTimeout 10s

Wait />$/
Type "dotnet run --project ../AsciiEarth/AsciiEarth.csproj -c Release"
Enter
Wait+Screen@120s /T tour/

Type "t"
Wait+Screen /tour:/
Sleep 5s

Ctrl+C
Wait />$/
