using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// An exhibit demonstrating Sixel graphics support with fallback for terminals
/// that don't support Sixel.
/// </summary>
public class SixelExhibit : Hex1bExhibit
{
    private readonly ILogger<SixelExhibit> _logger;
    private readonly IWebHostEnvironment _environment;

    public SixelExhibit(ILogger<SixelExhibit> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public override string Id => "sixel";
    public override string Title => "Sixel Graphics";
    public override string Description => "Sixel image rendering with automatic fallback for unsupported terminals.";

    /// <summary>
    /// Represents a sample image for the gallery.
    /// </summary>
    private class SampleImage
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string FilePath { get; init; }
        public string? CachedSixelData { get; set; }
        public int CachedWidth { get; set; }
        public int CachedHeight { get; set; }
    }

    /// <summary>
    /// State for the Sixel exhibit.
    /// </summary>
    private class SixelState
    {
        public ListState ImageList { get; } = new();
        public List<SampleImage> Images { get; } = [];
        
        public SampleImage? SelectedImage => 
            ImageList.SelectedIndex >= 0 && ImageList.SelectedIndex < Images.Count 
                ? Images[ImageList.SelectedIndex] 
                : null;
    }

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating widget builder for Sixel exhibit");

        var state = new SixelState();
        var imagesPath = Path.Combine(_environment.WebRootPath, "images");
        
        // Add real images from wwwroot/images
        if (Directory.Exists(imagesPath))
        {
            var imageFiles = Directory.GetFiles(imagesPath, "*.*")
                .Where(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var filePath in imageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                state.Images.Add(new SampleImage
                {
                    Id = fileName.ToLowerInvariant(),
                    Name = char.ToUpper(fileName[0]) + fileName[1..],
                    Description = $"Image: {Path.GetFileName(filePath)}",
                    FilePath = filePath
                });
            }
        }

        // If no images found, add a placeholder
        if (state.Images.Count == 0)
        {
            _logger.LogWarning("No images found in {Path}", imagesPath);
        }

        state.ImageList.Items = state.Images
            .Select(img => new ListItem(img.Id, img.Name))
            .ToList();

        return ct =>
        {
            var ctx = new RootContext<SixelState>(state);
            var selectedImage = state.SelectedImage;

            // Calculate available space for the image
            // Right panel gets about 60% of 80 cols = ~48 cols, minus borders = ~44 cols
            // Height is typically 24 rows minus headers = ~18 rows
            const int imageWidthCells = 44;
            const int imageHeightCells = 16;

            // Get or generate sixel data for the selected image
            string sixelData = "";
            if (selectedImage != null)
            {
                // Check if we need to regenerate (size changed or first time)
                if (selectedImage.CachedSixelData == null ||
                    selectedImage.CachedWidth != imageWidthCells ||
                    selectedImage.CachedHeight != imageHeightCells)
                {
                    try
                    {
                        _logger.LogInformation("Encoding {Image} at {W}x{H} cells", 
                            selectedImage.Name, imageWidthCells, imageHeightCells);
                        
                        sixelData = SixelEncoder.EncodeFromFile(
                            selectedImage.FilePath,
                            imageWidthCells,
                            imageHeightCells);
                        
                        // Cache the result
                        selectedImage.CachedSixelData = sixelData;
                        selectedImage.CachedWidth = imageWidthCells;
                        selectedImage.CachedHeight = imageHeightCells;
                        
                        _logger.LogInformation("Encoded {Image}: {Len} bytes", 
                            selectedImage.Name, sixelData.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to encode {Image}", selectedImage.Name);
                        sixelData = "";
                    }
                }
                else
                {
                    sixelData = selectedImage.CachedSixelData;
                }
            }

            var widget = ctx.Splitter(
                // Left panel: Image list
                ctx.Layout(leftPanel => [
                    leftPanel.Text("═══ Images ═══"),
                    leftPanel.Text(""),
                    leftPanel.List(s => s.ImageList),
                    leftPanel.Text(""),
                    leftPanel.Text("Use ↑↓ to select"),
                    leftPanel.Text("Tab to switch panels")
                ]),
                // Right panel: Image viewer
                ctx.Layout(rightPanel => selectedImage != null
                    ? [
                        rightPanel.Text($"═══ {selectedImage.Name} ═══"),
                        rightPanel.Text(""),
                        rightPanel.Text(selectedImage.Description),
                        rightPanel.Text(""),
                        string.IsNullOrEmpty(sixelData)
                            ? rightPanel.Text("[Failed to load image]")
                            : rightPanel.Sixel(
                                sixelData,
                                rightPanel.VStack(fallback => [
                                    fallback.Text("┌─────────────────────────────────┐"),
                                    fallback.Text("│  Sixel graphics not supported   │"),
                                    fallback.Text("│  in this terminal.              │"),
                                    fallback.Text("│                                 │"),
                                    fallback.Text("│  Try using a Sixel-capable      │"),
                                    fallback.Text("│  terminal like:                 │"),
                                    fallback.Text("│  • xterm -ti vt340              │"),
                                    fallback.Text("│  • mlterm                       │"),
                                    fallback.Text("│  • foot                         │"),
                                    fallback.Text("│  • WezTerm                      │"),
                                    fallback.Text("└─────────────────────────────────┘"),
                                ]),
                                width: imageWidthCells,
                                height: imageHeightCells)
                      ]
                    : [
                        rightPanel.Text("═══ No Image Selected ═══"),
                        rightPanel.Text(""),
                        rightPanel.Text("Select an image from the list"),
                        rightPanel.Text(""),
                        state.Images.Count == 0
                            ? rightPanel.Text("No images found in wwwroot/images/")
                            : rightPanel.Text($"Found {state.Images.Count} image(s)")
                      ]),
                leftWidth: 25
            );

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
