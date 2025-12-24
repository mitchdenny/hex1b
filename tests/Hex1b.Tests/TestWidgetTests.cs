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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

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
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = appInstance.RunAsync(cts.Token);

        // Wait for initial render then stop the app
        await firstRenderTcs.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        appInstance.RequestStop();

        await runTask;

        // The app does two render cycles: initial render, then one more after RequestStop (which calls Invalidate)
        // First cycle: new TestWidget created, reconciled (count=1), rendered (count=1)
        // Second cycle: same TestWidget reused, reconciled again (count=2) - reports ReconcileCount=1 because
        //   each TestWidget instance maintains its own count, but the widget is the same instance...
        //   Actually, each cycle creates a NEW TestWidget via ctx.Test(), so each starts at count=1.
        // With render optimization:
        // - First cycle: new node → dirty → rendered
        // - Second cycle: same node reused → not dirty → NOT rendered (optimization skips clean nodes)
        Assert.NotNull(initialNode);
        Assert.Equal([1, 1], reconcileCounts); // Two cycles, each widget starts at count 1
        Assert.Same(initialNode, lastExistingNode); // Second reconcile receives the node from first cycle
        Assert.Equal([1], renderCounts); // With render optimization, clean nodes don't re-render
    }
}
