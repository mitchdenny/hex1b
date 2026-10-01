using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Column definition for table layout, containing width hint and alignment.
/// </summary>
/// <param name="Width">The width hint for this column.</param>
/// <param name="Alignment">The horizontal alignment for cell content.</param>
internal record TableColumnDef(SizeHint Width, Alignment Alignment);
