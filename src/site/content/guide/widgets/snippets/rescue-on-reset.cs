ctx.Rescue(
    ctx.SomeWidget()
)
.OnRescue(e => logger.LogError(e.Exception, "Error caught"))
.OnReset(e => {
    logger.LogInformation("User retried after {Phase} error", e.Phase);
    ResetApplicationState();
})
