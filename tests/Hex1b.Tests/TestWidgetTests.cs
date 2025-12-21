using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hex1b;
using Hex1b.Widgets;
using Xunit;

namespace Hex1b.Tests;

public class TestWidgetTests
{
    [Fact]
    public async Task Test_FluentCallbacks_WorkWithHex1bApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        var renderCounts = new List<int>();
        var reconcileCounts = new List<int>();
        TestWidgetNode? initialNode = null;
        Hex1bNode? lastExistingNode = null;

        var firstRenderTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var appInstance = new Hex1bApp(
            ctx =>
            {
                var test = ctx.Test()
                    .OnReconcile(args =>
                    {
                        reconcileCounts.Add(args.ReconcileCount);
                        lastExistingNode = args.ExistingNode;
                        if (args.ReconcileCount == 1)
                        {
                            initialNode = args.Node;
                        }
                    })
                    .OnRender(args =>
                    {
                        renderCounts.Add(args.RenderCount);
                        firstRenderTcs.TrySetResult();
                    });

                return test;
            },
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = appInstance.RunAsync(cts.Token);

        // Wait for initial render then stop the app
        await firstRenderTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        appInstance.RequestStop();

        await runTask;

        // The app does two render cycles: initial render, then one more after RequestStop (which calls Invalidate)
        // Each cycle creates a new TestWidget, so each reports count=1
        Assert.NotNull(initialNode);
        Assert.Equal([1, 1], reconcileCounts); // Two cycles, each widget starts at count 1
        Assert.Same(initialNode, lastExistingNode); // Second reconcile receives the node from first cycle
        Assert.Equal([1, 1], renderCounts);
    }
}
