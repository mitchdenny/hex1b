ctx.Rescue(
    ctx.SomeWidget()
)
.OnRescue(e => {
    logger.LogError(e.Exception, "Error in {Phase}", e.Phase);
})
