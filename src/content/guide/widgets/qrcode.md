<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode  → src/Hex1b.Website/Examples/QrCodeBasicExample.cs
  - customCode → src/Hex1b.Website/Examples/QrCodeCustomExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/qrcode-basic.cs?raw'
import quietZoneSnippet from './snippets/qrcode-quietzone.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("QR Code Example"),
        v.Text(""),
        v.Text("Scan with your phone:"),
        v.QrCode("https://hex1b.dev"),
        v.Text(""),
        v.Text("The QR code encodes: https://hex1b.dev")
    ]))
    .Build();

await terminal.RunAsync();`

const customCode = `using Hex1b;

var state = new QrCodeState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Interactive QR Code Demo"),
        v.Text(""),
        v.Text($"URL: {state.CurrentUrl}"),
        v.Text(""),
        v.QrCode(state.CurrentUrl).WithQuietZone(state.QuietZone),
        v.Text(""),
        v.Text("Select URL:"),
        v.Picker(state.UrlOptions, state.SelectedUrlIndex)
            .OnSelectionChanged(e => {
                state.SelectedUrlIndex = e.SelectedIndex;
                state.CurrentUrl = state.UrlOptions[e.SelectedIndex];
            }),
        v.Text(""),
        v.HStack(h => [
            h.Text("Quiet Zone: "),
            h.Button("-").OnClick(_ => {
                if (state.QuietZone > 0) state.QuietZone--;
            }),
            h.Text($" {state.QuietZone} "),
            h.Button("+").OnClick(_ => {
                if (state.QuietZone < 4) state.QuietZone++;
            })
        ])
    ]))
    .Build();

await terminal.RunAsync();

class QrCodeState
{
    public string CurrentUrl { get; set; } = "https://github.com/mitchdenny/hex1b";
    public int QuietZone { get; set; } = 1;
    public string[] UrlOptions { get; } = [
        "https://github.com/mitchdenny/hex1b",
        "https://hex1b.dev",
        "https://dotnet.microsoft.com"
    ];
    public int SelectedUrlIndex { get; set; } = 0;
}`
</script>

# QrCodeWidget

Display scannable QR codes in your terminal using Unicode block characters (██).

QR codes are rendered as a grid of filled and empty blocks that can be scanned by mobile devices to quickly navigate to URLs, share WiFi credentials, or encode any text data. The primary use case is encoding URLs for easy access from mobile devices.

## Basic Usage

Create QR codes using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="qrcode-basic" exampleTitle="QR Code Widget - Basic Usage" />

The QR code is automatically sized based on the data length. Longer data produces larger QR codes with more modules (the black and white squares).

::: tip Terminal Support
QR codes work in all terminals that support Unicode block characters (██). The codes are scannable with standard QR code reader apps on smartphones.
:::

## Quiet Zone

The quiet zone is the white border around the QR code. By default, QrCodeWidget uses a quiet zone of 1 module width. You can customize this:

<StaticTerminalPreview svgPath="/svg/qrcode-quietzone.svg" :code="quietZoneSnippet" />

```csharp
// Default quiet zone (1 module)
v.QrCode("https://hex1b.dev")

// No quiet zone
v.QrCode("https://hex1b.dev").WithQuietZone(0)

// Larger quiet zone (4 modules)
v.QrCode("https://hex1b.dev").WithQuietZone(4)
```

::: info QR Code Standards
QR code specifications recommend a quiet zone of at least 4 modules for optimal scanning reliability. However, in controlled terminal environments where the QR code is displayed on a clean background, smaller quiet zones (1-2 modules) work well and save space.
:::

## Interactive Example

Here's a complete example with URL selection and quiet zone controls:

<CodeBlock lang="csharp" :code="customCode" command="dotnet run" example="qrcode-custom" exampleTitle="QR Code Widget - Interactive Demo" />

This example demonstrates:
- Dynamic QR code updates when the URL changes
- Picker widget for URL selection
- Buttons to adjust the quiet zone
- Real-time re-rendering as state changes

## Data Encoding

QrCodeWidget uses error correction level Q (Quartile), which provides 25% data recovery capability. This is a good balance between:
- **Error resilience**: Can recover from moderate damage or distortion
- **Data capacity**: Supports reasonable URL lengths without excessive QR code size

### Supported Data

While the primary use case is URLs, QrCodeWidget can encode any text data:

```csharp
// URLs
v.QrCode("https://github.com/mitchdenny/hex1b")

// Plain text
v.QrCode("Hello, World!")

// WiFi credentials (using standard format)
v.QrCode("WIFI:T:WPA;S:MyNetwork;P:MyPassword;;")

// Contact information
v.QrCode("BEGIN:VCARD\\nFN:John Doe\\nTEL:+1234567890\\nEND:VCARD")
```

::: warning Data Length Limits
QR codes have size limits based on error correction level and version. If the data is too long to encode, QrCodeWidget gracefully handles the error by displaying an empty area. Keep URLs under 200 characters for best results.
:::

## Rendering Details

QrCodeWidget renders QR codes using double-width Unicode block characters:
- **Filled modules**: `██` (full block character, doubled for square appearance)
- **Empty modules**: `  ` (two spaces)

This double-width approach ensures QR codes appear roughly square in most terminal fonts, which typically have character cells about twice as tall as they are wide.

### Size Calculation

The widget automatically measures itself based on:
1. The QR code matrix size (determined by data length and error correction)
2. The quiet zone on all four sides

For example, a 25×25 module QR code with a quiet zone of 1 becomes 27×27 characters in the terminal.

## Theming

QrCodeWidget respects theme colors from parent widgets:

```csharp
using Hex1b;
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(ThemeElement.ForegroundColor, Hex1bColor.Green)
    .Set(ThemeElement.BackgroundColor, Hex1bColor.Black);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => ctx.ThemePanel(tp => [
            tp.Text("Green QR Code:"),
            tp.QrCode("https://hex1b.dev")
        ])
        .WithForegroundColor(Hex1bColor.Green);
    })
    .Build();

await terminal.RunAsync();
```

The block characters inherit the foreground color from the theme, allowing you to create colored QR codes that still scan correctly.

## Use Cases

### Application URLs

Share links to documentation, repositories, or web applications:

```csharp
v.VStack(stack => [
    stack.Text("Visit our documentation:"),
    stack.QrCode("https://hex1b.dev/guide/getting-started")
])
```

### Configuration Sharing

Encode configuration data for easy transfer to mobile devices:

```csharp
var config = $"CONFIG:server={server};port={port};key={apiKey}";
v.QrCode(config)
```

### Status Dashboard

Display QR codes that link to detailed status pages or logs:

```csharp
v.HStack(row => [
    row.VStack(info => [
        info.Text($"Build: {buildNumber}"),
        info.Text($"Status: {status}")
    ]),
    row.QrCode($"https://build-server.com/build/{buildNumber}")
])
```

## Related Widgets

- [TextWidget](/guide/widgets/text) - For displaying text content
- [HyperlinkWidget](/guide/widgets/hyperlink) - For clickable terminal hyperlinks
- [PickerWidget](/guide/widgets/picker) - For selection lists (pairs well with QR codes)
