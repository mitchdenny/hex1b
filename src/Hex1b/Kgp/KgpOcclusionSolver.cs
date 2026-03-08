using Hex1b.Layout;

namespace Hex1b.Kgp;

/// <summary>
/// Computes visible KGP image fragments by subtracting occluding window rectangles.
/// </summary>
/// <remarks>
/// <para>
/// The solver processes each KGP image from the registry, subtracting all occluder
/// rectangles at higher layers. The result is a set of non-overlapping rectangular
/// fragments for each image, each mapped to pixel-level KGP clip coordinates
/// (<c>x</c>, <c>y</c>, <c>w</c>, <c>h</c> parameters in the placement command).
/// </para>
/// <para>
/// Rectangle subtraction of A\B produces 0-4 rectangular strips:
/// <code>
/// ┌────────────────────────┐
/// │      Top strip         │
/// ├──────┬────────┬────────┤
/// │ Left │Occluder│ Right  │
/// │strip │   B    │ strip  │
/// ├──────┴────────┴────────┤
/// │      Bottom strip      │
/// └────────────────────────┘
/// </code>
/// </para>
/// </remarks>
public static class KgpOcclusionSolver
{
    /// <summary>
    /// Computes visible fragments for all KGP images in the registry.
    /// </summary>
    public static List<KgpFragment> ComputeFragments(KgpImageRegistry registry)
    {
        var fragments = new List<KgpFragment>();

        foreach (var image in registry.Images)
        {
            var imageRect = new Rect(
                image.AbsoluteX,
                image.AbsoluteY,
                image.Data.WidthInCells,
                image.Data.HeightInCells);

            // Start with the full image as a single visible rectangle
            var visibleRects = new List<Rect> { imageRect };

            // Subtract all occluder rects at strictly higher layers
            foreach (var occluder in registry.Occluders)
            {
                if (occluder.Layer <= image.Layer)
                    continue; // Same or lower layer — no occlusion

                visibleRects = SubtractFromAll(visibleRects, occluder.Bounds);

                if (visibleRects.Count == 0)
                    break; // Fully occluded
            }

            // Convert visible rects to KGP fragments with clip coordinates
            foreach (var rect in visibleRects)
            {
                var fragment = CreateFragment(image, imageRect, rect);
                fragments.Add(fragment);
            }
        }

        return fragments;
    }

    /// <summary>
    /// Subtracts an occluder rectangle from each rectangle in the list.
    /// </summary>
    internal static List<Rect> SubtractFromAll(List<Rect> rects, Rect occluder)
    {
        var result = new List<Rect>();
        foreach (var rect in rects)
        {
            SubtractSingle(rect, occluder, result);
        }
        return result;
    }

    /// <summary>
    /// Subtracts occluder from a single rectangle, producing 0-4 non-overlapping strips.
    /// </summary>
    internal static void SubtractSingle(Rect rect, Rect occluder, List<Rect> result)
    {
        // Compute overlap
        var overlapLeft = Math.Max(rect.X, occluder.X);
        var overlapTop = Math.Max(rect.Y, occluder.Y);
        var overlapRight = Math.Min(rect.X + rect.Width, occluder.X + occluder.Width);
        var overlapBottom = Math.Min(rect.Y + rect.Height, occluder.Y + occluder.Height);

        // No overlap — keep original rect
        if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
        {
            result.Add(rect);
            return;
        }

        // Fully covered — nothing remains
        if (overlapLeft == rect.X && overlapTop == rect.Y &&
            overlapRight == rect.X + rect.Width && overlapBottom == rect.Y + rect.Height)
        {
            return;
        }

        // Top strip (full width, above the overlap)
        if (overlapTop > rect.Y)
        {
            result.Add(new Rect(rect.X, rect.Y, rect.Width, overlapTop - rect.Y));
        }

        // Bottom strip (full width, below the overlap)
        if (overlapBottom < rect.Y + rect.Height)
        {
            result.Add(new Rect(rect.X, overlapBottom, rect.Width, rect.Y + rect.Height - overlapBottom));
        }

        // Left strip (between top and bottom, left of overlap)
        if (overlapLeft > rect.X)
        {
            result.Add(new Rect(rect.X, overlapTop, overlapLeft - rect.X, overlapBottom - overlapTop));
        }

        // Right strip (between top and bottom, right of overlap)
        if (overlapRight < rect.X + rect.Width)
        {
            result.Add(new Rect(overlapRight, overlapTop, rect.X + rect.Width - overlapRight, overlapBottom - overlapTop));
        }
    }

    /// <summary>
    /// Creates a KGP fragment from a visible rectangle within the original image bounds.
    /// Maps cell-level offsets to pixel-level KGP clip coordinates.
    /// </summary>
    internal static KgpFragment CreateFragment(KgpImageEntry image, Rect imageRect, Rect visibleRect)
    {
        var data = image.Data;

        // If the visible rect is the entire image, no clipping needed
        if (visibleRect.X == imageRect.X && visibleRect.Y == imageRect.Y &&
            visibleRect.Width == imageRect.Width && visibleRect.Height == imageRect.Height)
        {
            return new KgpFragment(
                data.ImageId,
                visibleRect.X,
                visibleRect.Y,
                visibleRect.Width,
                visibleRect.Height,
                data.ClipX,
                data.ClipY,
                data.ClipW,
                data.ClipH,
                data);
        }

        // Cell offset from image origin
        var cellOffsetX = visibleRect.X - imageRect.X;
        var cellOffsetY = visibleRect.Y - imageRect.Y;

        // Effective source pixel dimensions (accounting for existing clip)
        var effectiveW = data.ClipW > 0 ? data.ClipW : (int)data.SourcePixelWidth;
        var effectiveH = data.ClipH > 0 ? data.ClipH : (int)data.SourcePixelHeight;

        // Map cell offsets to pixel coordinates.
        // Compute clip width/height from endpoint differences rather than independently,
        // so adjacent fragments tile perfectly without gaps from integer truncation.
        // Example: 128px in 30 cells → fragments at offsets [0,10) and [10,15) get
        //   clipX₁=0, clipEnd₁=42 → clipW₁=42    clipX₂=42, clipEnd₂=64 → clipW₂=22
        // vs the old way: clipW₁=42, clipW₂=21 (gap at pixel 63-64).
        var clipX = data.ClipX + (int)((long)cellOffsetX * effectiveW / imageRect.Width);
        var clipY = data.ClipY + (int)((long)cellOffsetY * effectiveH / imageRect.Height);
        var clipEndX = data.ClipX + (int)((long)(cellOffsetX + visibleRect.Width) * effectiveW / imageRect.Width);
        var clipEndY = data.ClipY + (int)((long)(cellOffsetY + visibleRect.Height) * effectiveH / imageRect.Height);
        var clipW = clipEndX - clipX;
        var clipH = clipEndY - clipY;

        return new KgpFragment(
            data.ImageId,
            visibleRect.X,
            visibleRect.Y,
            visibleRect.Width,
            visibleRect.Height,
            clipX,
            clipY,
            clipW,
            clipH,
            data);
    }
}
