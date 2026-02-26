using Hex1b;
using Hex1b.Kgp;
using Hex1b.Widgets;

// First, do a raw KGP test bypassing all Hex1b infrastructure
// This proves the terminal supports KGP
Console.WriteLine("=== Raw KGP Test (bypasses Hex1b) ===");
Console.WriteLine($"TERM_PROGRAM={Environment.GetEnvironmentVariable("TERM_PROGRAM")}");
Console.WriteLine($"TERM={Environment.GetEnvironmentVariable("TERM")}");
Console.WriteLine();

// Generate a tiny 4x4 RGBA test image (solid red)
var rawPixels = new byte[4 * 4 * 4];
for (int i = 0; i < rawPixels.Length; i += 4)
{
    rawPixels[i] = 255;     // R
    rawPixels[i + 1] = 0;   // G
    rawPixels[i + 2] = 0;   // B
    rawPixels[i + 3] = 255; // A
}
var base64 = Convert.ToBase64String(rawPixels);

Console.WriteLine("Sending raw KGP transmit+display (should show red square):");
// Raw KGP: a=T (transmit+display), f=32 (RGBA), s=4 (width), v=4 (height), c=8 (display cols), r=4 (display rows)
Console.Write($"\x1b_Ga=T,f=32,s=4,v=4,i=99,c=8,r=4,q=2;{base64}\x1b\\");
Console.WriteLine();
Console.WriteLine();

Console.WriteLine("If you see a red square above, KGP works in your terminal.");
Console.WriteLine("Press Enter to continue to Hex1b passthrough test...");
Console.ReadLine();

// Test 2: Send KGP through Hex1bTerminal using a raw workload (no widgets/surfaces)
// This tests the terminal output pump path only
Console.WriteLine("=== Hex1b Terminal Passthrough Test ===");
Console.WriteLine("Sending KGP through Hex1bTerminal via StreamWorkloadAdapter...");

{
    using var outputPipe = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.Out);
    using var outputReader = new System.IO.Pipes.AnonymousPipeClientStream(System.IO.Pipes.PipeDirection.In,
        outputPipe.ClientSafePipeHandle);
    using var inputSink = new MemoryStream();

    var workload = new StreamWorkloadAdapter(outputReader, inputSink);
    await using var passTerminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithDimensions(80, 24)
        .Build();

    using var cts = new CancellationTokenSource();
    var pumpTask = passTerminal.RunAsync(cts.Token);

    // Write the raw KGP sequence through the workload pipe
    var kgpSeq = $"\x1b_Ga=T,f=32,s=4,v=4,i=98,c=8,r=4,q=2;{base64}\x1b\\";
    var kgpBytes = System.Text.Encoding.UTF8.GetBytes(kgpSeq);
    outputPipe.Write(kgpBytes);
    outputPipe.Flush();

    // Give pump time to forward to presentation
    await Task.Delay(1000);

    cts.Cancel();
    try { await pumpTask; } catch (OperationCanceledException) { }
}

Console.WriteLine();
Console.WriteLine("If you see a red square above, Hex1bTerminal passthrough works.");
Console.WriteLine("Press Enter to continue to Hex1b widget test...");
Console.ReadLine();

// Test 3: Full Hex1b widget test
// Now test with Hex1b widget system
const uint imageWidth = 32;
const uint imageHeight = 32;
var pixelData = GenerateTestPattern(imageWidth, imageHeight);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Kitty Graphics Protocol (KGP) Demo"),
        v.Separator(),
        v.Text(""),
        v.Text("32×32 test pattern (gradient + color blocks):"),
        v.KittyGraphics(pixelData, imageWidth, imageHeight)
            .WithDisplaySize(16, 8),
        v.Text(""),
        v.Text("Same image at different sizes:"),
        v.HStack(h => [
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(8, 4),
            h.Text("  "),
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(4, 2),
            h.Text("  "),
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(2, 1)
        ]),
        v.Text(""),
        v.Separator(),
        v.Text("Press Ctrl+C to exit")
    ]))
    .Build();

await terminal.RunAsync();

/// <summary>
/// Generates a 32×32 RGBA test pattern with color gradients and blocks.
/// </summary>
static byte[] GenerateTestPattern(uint width, uint height)
{
    var data = new byte[width * height * 4];
    for (uint y = 0; y < height; y++)
    {
        for (uint x = 0; x < width; x++)
        {
            var offset = (int)((y * width + x) * 4);
            var quadrantX = x < width / 2 ? 0 : 1;
            var quadrantY = y < height / 2 ? 0 : 1;
            var quadrant = quadrantY * 2 + quadrantX;

            switch (quadrant)
            {
                case 0: // Top-left: red gradient
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = 0;
                    data[offset + 2] = 0;
                    break;
                case 1: // Top-right: green gradient
                    data[offset] = 0;
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
                case 2: // Bottom-left: blue gradient
                    data[offset] = 0;
                    data[offset + 1] = 0;
                    data[offset + 2] = (byte)(x * 255 / width);
                    break;
                case 3: // Bottom-right: yellow gradient
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
            }

            data[offset + 3] = 255; // Full alpha
        }
    }

    return data;
}
