using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Quick "what is this thing?" intro for audiences who haven't seen Hex1b.
/// </summary>
internal sealed class WhatIsHex1bSlide : ISlide
{
    public string Title => "What is Hex1b?";

    public Hex1bWidget Build(SlideContext context) =>
        PlaceholderSlide.Build(
            context,
            "What is Hex1b?",
            "A .NET library for building terminal UIs",
            "React-inspired declarative API (widgets + nodes)",
            "Targets net10.0, ships to NuGet as `Hex1b`",
            "Yes — this presentation tool is itself a Hex1b app");
}
