using System;
using Hex1b;
using Hex1b.Layout;

namespace Hex1b.Tests;

internal sealed class TestWidgetNode : Hex1bNode
{
    internal Action? RenderCallback { get; set; }

    protected override Size MeasureCore(Constraints constraints) => Size.Zero;

    public override void Render(Hex1bRenderContext context)
    {
        RenderCallback?.Invoke();
    }
}
