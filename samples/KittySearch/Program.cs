using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

const int resultCount = 4;
var datasetPath = Path.Combine(AppContext.BaseDirectory, "data");
using var state = new TgifSearchState(TgifDataset.Open(datasetPath), resultCount);
var supportsNativeAnimation = TerminalAnimationSupport.SupportsNativeKgpAnimation();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(
        options => options.FrameRateLimitMs = 33,
        app =>
        {
            state.App = app;
            return context => context.VStack(outer =>
            [
                outer.Text(" KittySearch"),
                outer.Text(" Search descriptions. Hover to animate; click to copy a playback command."),
                outer.HStack(row =>
                [
                    row.Text(" Search: ").ContentWidth(),
                    row.TextBox(state.Query)
                        .OnTextChanged(args => state.Search(args.NewText))
                        .Fill(),
                ]).ContentHeight(),
                outer.Text($" {state.Status}").ContentHeight(),
                outer.Border(panel => BuildResults(panel, state, supportsNativeAnimation))
                    .Title("Results")
                    .Fill(),
                outer.Text(" Try: cat, dog, dancing, happy, laugh, baby, wave, smile")
                    .ContentHeight(),
            ]);
        })
    .Build();

await terminal.RunAsync();

static Hex1bWidget[] BuildResults(
    WidgetContext<VStackWidget> context,
    TgifSearchState state,
    bool supportsNativeAnimation)
{
    var results = state.Results;
    if (results.Count == 0)
    {
        return [context.Text(" Type a keyword to search the bundled animations.")];
    }

    return
    [
        context.HStack(row =>
        [
            .. results.Select(result =>
            {
                var interactable = row.Interactable(image =>
                    image.Border(
                        image.VStack(card =>
                        {
                            Hex1bWidget media;
                            if (state.IsFlashing(result))
                            {
                                media = card.KgpImage(
                                        result.GetWhiteFrame(),
                                        result.PixelWidth,
                                        result.PixelHeight,
                                        fallback => fallback.Text("[KGP unavailable]"))
                                    .Fit()
                                    .AboveText();
                            }
                            else if (image.IsHovered && supportsNativeAnimation)
                            {
                                media = card.KgpAnimation(
                                        result.GetAnimationFrames(),
                                        result.PixelWidth,
                                        result.PixelHeight,
                                        fallback => fallback.Text("[KGP unavailable]"))
                                    .Playing()
                                    .Fit()
                                    .AboveText();
                            }
                            else if (image.IsHovered)
                            {
                                var frame = state.GetSoftwareAnimationFrame(result);
                                media = card.KgpImage(
                                        frame.Data,
                                        result.PixelWidth,
                                        result.PixelHeight,
                                        fallback => fallback.Text("[KGP unavailable]"))
                                    .Fit()
                                    .AboveText();
                            }
                            else
                            {
                                media = card.KgpImage(
                                        result.PreviewData,
                                        result.PixelWidth,
                                        result.PixelHeight,
                                        fallback => fallback.Text("[KGP unavailable]"))
                                    .Fit()
                                    .AboveText();
                            }

                            return
                            [
                                media.FixedWidth(22).FixedHeight(8),
                                card.Text(Truncate(result.Description, 22)).ContentHeight(),
                            ];
                        })));

                if (!supportsNativeAnimation)
                {
                    interactable = interactable.OnHoverChanged(
                        args => state.SetSoftwareAnimation(result, args.IsHovered));
                }

                interactable = interactable.OnClick(args =>
                {
                    var command = result.GetPlaybackCommand(supportsNativeAnimation);
                    args.Context.CopyToClipboard(command);
                    state.ShowCopiedFlash(result, supportsNativeAnimation);
                });

                return interactable
                    .FixedWidth(24)
                    .FixedHeight(11);
            })
        ])
    ];
}

static string Truncate(string value, int width)
    => value.Length <= width ? value : value[..(width - 1)] + "…";
