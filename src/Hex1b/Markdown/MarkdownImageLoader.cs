namespace Hex1b.Markdown;

/// <summary>
/// Callback for loading and decoding markdown images.
/// The <paramref name="imageUri"/> is parsed from the image source attribute using
/// <see cref="UriKind.RelativeOrAbsolute"/>, supporting both local file paths and remote URLs.
/// Return <c>null</c> to fall back to text-only rendering of the image alt text.
/// </summary>
public delegate Task<MarkdownImageData?> MarkdownImageLoader(Uri imageUri, string altText);
