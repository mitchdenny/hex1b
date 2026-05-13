namespace Hex1b;

/// <summary>
/// Builder extensions for configuring a placeholder workload that drives the
/// terminal until the real ("primary") workload signals it is ready.
/// </summary>
public static class PlaceholderWorkloadBuilderExtensions
{
    /// <summary>
    /// Wraps the already-configured workload (the <em>primary</em>) with a
    /// <see cref="PlaceholderWorkloadAdapter"/> that initially streams from
    /// the <em>placeholder</em> workload built by <paramref name="configurePlaceholder"/>,
    /// then swaps to the primary once it is ready.
    /// </summary>
    /// <param name="builder">The terminal builder whose primary workload is being decorated.</param>
    /// <param name="configurePlaceholder">
    /// Configures the placeholder using a fresh <see cref="Hex1bTerminalBuilder"/>.
    /// Only workload-shaped configuration is honoured (e.g. <c>WithHex1bApp</c>);
    /// presentation, mouse, scrollback and filter settings on the inner builder
    /// are ignored — the outer terminal owns those.
    /// </param>
    /// <param name="resumePolicy">
    /// What to do if the primary workload disconnects after going active.
    /// Defaults to <see cref="PlaceholderResumePolicy.OnDisconnect"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no primary workload has been configured on
    /// <paramref name="builder"/> before this call.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The placeholder is treated as the source of truth for the screen until
    /// the primary's <see cref="IConnectableWorkloadAdapter.ConnectedTask"/>
    /// completes (or the primary first appears, for non-connectable workloads).
    /// On each swap a terminal-reset sequence is prepended to the new active's
    /// bytes so the parser drops modes / SGR / scroll-region / alt-screen state
    /// from the previous occupant.
    /// </para>
    /// <para>
    /// This must be called <strong>after</strong> the primary workload has been
    /// configured (e.g. via <c>WithHmp1UdsClient</c>, <c>WithShellProcess</c>,
    /// <c>WithHex1bApp</c>, etc.). Calling it earlier throws.
    /// </para>
    /// </remarks>
    public static Hex1bTerminalBuilder WithPlaceholderWorkload(
        this Hex1bTerminalBuilder builder,
        Action<Hex1bTerminalBuilder> configurePlaceholder,
        PlaceholderResumePolicy resumePolicy = PlaceholderResumePolicy.OnDisconnect)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurePlaceholder);

        var primaryFactory = builder.GetConfiguredWorkloadFactory();
        var primaryAdapter = builder.GetConfiguredWorkloadAdapter();

        if (primaryFactory is null && primaryAdapter is null)
        {
            throw new InvalidOperationException(
                "WithPlaceholderWorkload must be called after the primary workload has been configured " +
                "(e.g. WithHmp1UdsClient, WithShellProcess, WithHex1bApp).");
        }

        // Build the placeholder eagerly through its own builder so callers
        // can use the standard With* vocabulary; we only need the resulting
        // workload adapter — presentation/scrollback/etc. on the inner
        // builder are ignored on purpose.
        var placeholderBuilder = new Hex1bTerminalBuilder();
        configurePlaceholder(placeholderBuilder);

        var placeholderFactory = placeholderBuilder.GetConfiguredWorkloadFactory();
        var placeholderAdapterDirect = placeholderBuilder.GetConfiguredWorkloadAdapter();

        if (placeholderFactory is null && placeholderAdapterDirect is null)
        {
            throw new InvalidOperationException(
                "WithPlaceholderWorkload's configurePlaceholder callback must configure a workload on the inner builder.");
        }

        builder.ClearConfiguredWorkload();
        builder.SetWorkloadFactory(presentation =>
        {
            IHex1bTerminalWorkloadAdapter primary;
            Func<CancellationToken, Task<int>>? primaryRun = null;
            if (primaryFactory != null)
            {
                var ctx = primaryFactory(presentation);
                primary = ctx.WorkloadAdapter;
                primaryRun = ctx.RunCallback;
            }
            else
            {
                primary = primaryAdapter!;
            }

            IHex1bTerminalWorkloadAdapter placeholder;
            Func<CancellationToken, Task<int>>? placeholderRun = null;
            if (placeholderFactory != null)
            {
                // Placeholder shouldn't see the outer presentation adapter — it
                // doesn't drive the screen directly; it produces bytes consumed
                // by PlaceholderWorkloadAdapter.
                var ctx = placeholderFactory(null);
                placeholder = ctx.WorkloadAdapter;
                placeholderRun = ctx.RunCallback;
            }
            else
            {
                placeholder = placeholderAdapterDirect!;
            }

            var wrapped = new PlaceholderWorkloadAdapter(primary, placeholder, placeholderRun, resumePolicy);

            // Compose a terminal-level run callback. The primary's own run
            // callback (e.g. HMP1's "ConnectAsync then await DisconnectedTask")
            // returns the moment the producer goes away — but most workload
            // adapters (HMP1 included) are one-shot and can't be reconnected
            // on the same instance. Under OnDisconnect we therefore rebuild
            // the primary via its factory and hand the fresh instance to the
            // wrapper, which re-runs its connected/disconnected watcher and
            // re-streams output from the new primary as soon as it connects.
            // Loop terminates only when ct is cancelled (Q / Ctrl-C) or the
            // resume policy is OneShot.
            Func<CancellationToken, Task<int>>? composedRun;
            if (primaryRun is null)
            {
                composedRun = null;
            }
            else if (resumePolicy == PlaceholderResumePolicy.OneShot
                     || primaryFactory is null)
            {
                // OneShot: legacy "primary disconnect = terminal exit".
                // Or no factory available (caller passed a pre-built adapter,
                // not a factory) — we have no way to rebuild, so behave as
                // OneShot regardless of policy.
                composedRun = primaryRun;
            }
            else
            {
                var currentRun = primaryRun;
                composedRun = async ct =>
                {
                    var lastExit = 0;
                    while (true)
                    {
                        try
                        {
                            lastExit = await currentRun(ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch
                        {
                            // Connect / read failures bubble out of primaryRun;
                            // swallow them so the placeholder UI keeps running
                            // and the next rebuild gets a chance to reconnect.
                        }

                        if (ct.IsCancellationRequested) break;

                        // Rebuild the primary and hand it to the wrapper.
                        var newCtx = primaryFactory(presentation);
                        await wrapped.ReplacePrimaryAsync(newCtx.WorkloadAdapter, ct)
                            .ConfigureAwait(false);
                        currentRun = newCtx.RunCallback ?? (_ => Task.FromResult(0));
                    }
                    return lastExit;
                };
            }

            return new Hex1bTerminalBuildContext(wrapped, composedRun);
        });

        return builder;
    }

    /// <summary>
    /// Convenience overload that configures a <see cref="Hex1bApp"/> as the placeholder.
    /// </summary>
    /// <param name="builder">The terminal builder whose primary workload is being decorated.</param>
    /// <param name="configure">A standard <c>WithHex1bApp</c>-shaped configuration callback.</param>
    /// <param name="configureOptions">Optional <see cref="Hex1bAppOptions"/> configurator.</param>
    /// <param name="resumePolicy">
    /// What to do if the primary workload disconnects after going active.
    /// Defaults to <see cref="PlaceholderResumePolicy.OnDisconnect"/>.
    /// </param>
    public static Hex1bTerminalBuilder WithPlaceholderHex1bApp(
        this Hex1bTerminalBuilder builder,
        Func<Hex1bApp, Func<RootContext, Hex1b.Widgets.Hex1bWidget>> configure,
        Action<Hex1bAppOptions>? configureOptions = null,
        PlaceholderResumePolicy resumePolicy = PlaceholderResumePolicy.OnDisconnect)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var opts = configureOptions ?? (_ => { });
        return builder.WithPlaceholderWorkload(b => b.WithHex1bApp(opts, configure), resumePolicy);
    }
}
