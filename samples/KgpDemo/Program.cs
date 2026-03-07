// KgpDemo - Demonstrates KGP (Kitty Graphics Protocol) support across the Hex1b stack.
//
// Scene 1: Surface Cell Basics
//   Validates KgpCellData z-index parameterization, clipping, and placement building.
//
// Additional scenes are added as each phase of KGP support is implemented.

using System.Security.Cryptography;
using Hex1b;

// Generate a small RGBA32 test image (4x4 pixels, gradient)
var pixelWidth = 4;
var pixelHeight = 4;
var imageData = GenerateGradientImage(pixelWidth, pixelHeight);

Console.WriteLine("KGP Demo - Scene 1: KGP Cell Data Basics");
Console.WriteLine("=========================================");
Console.WriteLine();

// Create KGP cell data with z=-1 (below text, default)
var hash = SHA256.HashData(imageData);
var base64Payload = Convert.ToBase64String(imageData);
var transmitPayload = $"\x1b_Ga=t,f=32,s={pixelWidth},v={pixelHeight},i=1,q=2;{base64Payload}\x1b\\";
var kgpBelowText = new KgpCellData(
    transmitPayload: transmitPayload,
    imageId: 1,
    widthInCells: 4,
    heightInCells: 2,
    sourcePixelWidth: (uint)pixelWidth,
    sourcePixelHeight: (uint)pixelHeight,
    contentHash: hash,
    zIndex: -1);

Console.WriteLine("=== Below-Text Image (z=-1) ===");
Console.WriteLine($"  ZIndex: {kgpBelowText.ZIndex}");
Console.WriteLine($"  Placement: {Sanitize(kgpBelowText.BuildPlacementPayload())}");
Console.WriteLine();

// Create KGP cell data with z=1 (above text)
var kgpAboveText = new KgpCellData(
    transmitPayload: transmitPayload,
    imageId: 2,
    widthInCells: 4,
    heightInCells: 2,
    sourcePixelWidth: (uint)pixelWidth,
    sourcePixelHeight: (uint)pixelHeight,
    contentHash: hash,
    zIndex: 1);

Console.WriteLine("=== Above-Text Image (z=1) ===");
Console.WriteLine($"  ZIndex: {kgpAboveText.ZIndex}");
Console.WriteLine($"  Placement: {Sanitize(kgpAboveText.BuildPlacementPayload())}");
Console.WriteLine();

// Test clipping preserves z-index
var clipped = kgpBelowText.WithClip(2, 2, 10, 8, 2, 1);
Console.WriteLine("=== Clipped Image ===");
Console.WriteLine($"  ZIndex preserved: {clipped.ZIndex == kgpBelowText.ZIndex} (z={clipped.ZIndex})");
Console.WriteLine($"  IsClipped: {clipped.IsClipped}");
Console.WriteLine($"  ClipRect: ({clipped.ClipX}, {clipped.ClipY}, {clipped.ClipW}, {clipped.ClipH})");
Console.WriteLine($"  New size: {clipped.WidthInCells}x{clipped.HeightInCells} cells");
Console.WriteLine($"  Placement: {Sanitize(clipped.BuildPlacementPayload())}");
Console.WriteLine();

// Demonstrate content hash for deduplication
Console.WriteLine("=== Deduplication ===");
Console.WriteLine($"  Hash match (same content): {KgpCellData.HashEquals(kgpBelowText.ContentHash, kgpAboveText.ContentHash)}");
var differentImage = GenerateGradientImage(8, 8);
var differentHash = SHA256.HashData(differentImage);
Console.WriteLine($"  Hash match (diff content): {KgpCellData.HashEquals(hash, differentHash)}");
Console.WriteLine();

Console.WriteLine("Scene 1 complete.");

static byte[] GenerateGradientImage(int width, int height)
{
    var data = new byte[width * height * 4]; // RGBA32
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            data[offset] = (byte)(x * 255 / Math.Max(1, width - 1));     // R
            data[offset + 1] = (byte)(y * 255 / Math.Max(1, height - 1)); // G
            data[offset + 2] = 128;                                        // B
            data[offset + 3] = 255;                                        // A
        }
    }
    return data;
}

static string Sanitize(string s) => s.Replace("\x1b", "ESC").Replace("\\", "\\\\");
