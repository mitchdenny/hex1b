namespace Hex1b;

internal sealed record KgpAnimationPlaybackSnapshot(
    uint ImageId,
    uint ImageNumber,
    int CurrentFrameNumber,
    KgpParsedCommand.AnimationPlaybackState PlaybackState,
    uint MaximumLoops,
    uint CompletedLoops,
    long? ElapsedTicks);
