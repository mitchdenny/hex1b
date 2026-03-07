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
Console.WriteLine();

// --- Scene 2: Capability Detection ---
Console.WriteLine("KGP Demo - Scene 2: Capability Detection");
Console.WriteLine("=========================================");
Console.WriteLine();

// Show what the Modern preset supports (baseline for SurfaceRenderContext)
var modern = TerminalCapabilities.Modern;
Console.WriteLine("=== TerminalCapabilities.Modern ===");
Console.WriteLine($"  SupportsKgp:       {modern.SupportsKgp}");
Console.WriteLine($"  SupportsSixel:     {modern.SupportsSixel}");
Console.WriteLine($"  SupportsTrueColor: {modern.SupportsTrueColor}");
Console.WriteLine();

// Show custom capabilities with KGP enabled
var kgpCaps = new TerminalCapabilities
{
    SupportsKgp = true,
    SupportsSixel = true,
    SupportsTrueColor = true,
    Supports256Colors = true,
};
Console.WriteLine("=== Custom KGP Capabilities ===");
Console.WriteLine($"  SupportsKgp:       {kgpCaps.SupportsKgp}");
Console.WriteLine($"  SupportsSixel:     {kgpCaps.SupportsSixel}");
Console.WriteLine($"  SupportsTrueColor: {kgpCaps.SupportsTrueColor}");
Console.WriteLine();

// Demonstrate SurfaceRenderContext capability propagation
var testSurface = new Hex1b.Surfaces.Surface(10, 5);
var ctx = new Hex1b.Surfaces.SurfaceRenderContext(testSurface);
Console.WriteLine("=== SurfaceRenderContext (default) ===");
Console.WriteLine($"  SupportsKgp: {ctx.Capabilities.SupportsKgp}");

ctx.SetCapabilities(kgpCaps);
Console.WriteLine("=== SurfaceRenderContext (after SetCapabilities) ===");
Console.WriteLine($"  SupportsKgp: {ctx.Capabilities.SupportsKgp}");
Console.WriteLine();

// Conditional rendering decision
Console.WriteLine("=== Conditional Rendering ===");
if (ctx.Capabilities.SupportsKgp)
    Console.WriteLine("  → Would render KGP image");
else if (ctx.Capabilities.SupportsSixel)
    Console.WriteLine("  → Would render Sixel fallback");
else
    Console.WriteLine("  → Would render text fallback");

Console.WriteLine();
Console.WriteLine("Scene 2 complete.");
Console.WriteLine();

// --- Scene 3: Layer Compositing Concepts ---
Console.WriteLine("KGP Demo - Scene 3: Layer Compositing");
Console.WriteLine("======================================");
Console.WriteLine();

// Demonstrate the z-ordering model with KGP data objects
Console.WriteLine("=== Z-Ordering Model ===");
Console.WriteLine("  KGP images can be placed below text (z < 0) or above text (z > 0).");
Console.WriteLine("  The compositing system assigns z-indexes by layer position.");
Console.WriteLine();

// Create KGP images at different z-levels
var bgImage = GenerateGradientImage(200, 160);
var bgHash = SHA256.HashData(bgImage);
var bgPayload = $"\x1b_Ga=t,f=32,s=200,v=160,i=10;{Convert.ToBase64String(bgImage)}\x1b\\";
var bgKgp = new KgpCellData(
    transmitPayload: bgPayload,
    imageId: 10,
    widthInCells: 20,
    heightInCells: 8,
    sourcePixelWidth: 200,
    sourcePixelHeight: 160,
    contentHash: bgHash,
    zIndex: -2);

var overlayImage = GenerateGradientImage(40, 40);
var ovHash = SHA256.HashData(overlayImage);
var ovPayload = $"\x1b_Ga=t,f=32,s=40,v=40,i=11;{Convert.ToBase64String(overlayImage)}\x1b\\";
var ovKgp = new KgpCellData(
    transmitPayload: ovPayload,
    imageId: 11,
    widthInCells: 4,
    heightInCells: 2,
    sourcePixelWidth: 40,
    sourcePixelHeight: 40,
    contentHash: ovHash,
    zIndex: 1);

Console.WriteLine($"  Background layer: {bgKgp.WidthInCells}x{bgKgp.HeightInCells} cells, z={bgKgp.ZIndex} (below text)");
Console.WriteLine($"  Overlay layer:    {ovKgp.WidthInCells}x{ovKgp.HeightInCells} cells, z={ovKgp.ZIndex} (above text)");
Console.WriteLine($"  Text layer:       renders at z=0 (between the two images)");
Console.WriteLine();

// Demonstrate placement building for each layer
Console.WriteLine("=== Background Placement ===");
Console.WriteLine($"  {Sanitize(bgKgp.BuildPlacementPayload())}");
Console.WriteLine();
Console.WriteLine("=== Overlay Placement ===");
Console.WriteLine($"  {Sanitize(ovKgp.BuildPlacementPayload())}");

Console.WriteLine();
Console.WriteLine("Scene 3 complete.");
Console.WriteLine();

// --- Scene 4: Clipping ---
Console.WriteLine("KGP Demo - Scene 4: Clipping");
Console.WriteLine("=============================");
Console.WriteLine();

// Demonstrate how WithClip creates different source rectangles from the same image
var fullImage = GenerateGradientImage(100, 100);
var fullHash = SHA256.HashData(fullImage);
var fullPayload = $"\x1b_Ga=t,f=32,s=100,v=100,i=20;{Convert.ToBase64String(fullImage)}\x1b\\";
var fullKgp = new KgpCellData(
    transmitPayload: fullPayload,
    imageId: 20,
    widthInCells: 10,
    heightInCells: 5,
    sourcePixelWidth: 100,
    sourcePixelHeight: 100,
    contentHash: fullHash,
    zIndex: -1);

Console.WriteLine($"  Original image: {fullKgp.WidthInCells}x{fullKgp.HeightInCells} cells ({fullKgp.SourcePixelWidth}x{fullKgp.SourcePixelHeight} px)");
Console.WriteLine();

// Clip to show only the top-left quadrant
var topLeft = fullKgp.WithClip(0, 0, 50, 50, 5, 3);
Console.WriteLine("=== Top-Left Quadrant ===");
Console.WriteLine($"  Clip: ({topLeft.ClipX}, {topLeft.ClipY}, {topLeft.ClipW}, {topLeft.ClipH})");
Console.WriteLine($"  Cell size: {topLeft.WidthInCells}x{topLeft.HeightInCells}");
Console.WriteLine($"  ZIndex preserved: {topLeft.ZIndex == fullKgp.ZIndex}");
Console.WriteLine($"  Placement: {Sanitize(topLeft.BuildPlacementPayload())}");
Console.WriteLine();

// Clip to show only the bottom-right quadrant
var bottomRight = fullKgp.WithClip(50, 50, 50, 50, 5, 3);
Console.WriteLine("=== Bottom-Right Quadrant ===");
Console.WriteLine($"  Clip: ({bottomRight.ClipX}, {bottomRight.ClipY}, {bottomRight.ClipW}, {bottomRight.ClipH})");
Console.WriteLine($"  Cell size: {bottomRight.WidthInCells}x{bottomRight.HeightInCells}");
Console.WriteLine($"  Placement: {Sanitize(bottomRight.BuildPlacementPayload())}");
Console.WriteLine();

// Show that all clips share the same ImageId
Console.WriteLine("=== Transmit-Once, Place-Many ===");
Console.WriteLine($"  Full image ID:      {fullKgp.ImageId}");
Console.WriteLine($"  Top-left clip ID:   {topLeft.ImageId}");
Console.WriteLine($"  Bottom-right clip:  {bottomRight.ImageId}");
Console.WriteLine($"  All same image: {fullKgp.ImageId == topLeft.ImageId && topLeft.ImageId == bottomRight.ImageId}");

Console.WriteLine();
Console.WriteLine("Scene 4 complete.");

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
