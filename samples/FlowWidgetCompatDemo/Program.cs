using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;
using FlowWidgetCompatDemo.Scenarios;

// All available widget test scenarios
var scenarios = new IWidgetScenario[]
{
    new TableCompactScenario(),
    new TableFullScenario(),
    new ListScenario(),
    new TreeScenario(),
    new TextBoxScenario(),
    new EditorScenario(),
    new BasicInputsScenario(),
    new PickerScenario(),
    new TabPanelScenario(),
    new AccordionScenario(),
    new MenuBarScenario(),
    new DrawerScenario(),
    new GridLayoutScenario(),
    new ScrollPanelScenario(),
    new SplitterScenario(),
    new ChartsScenario(),
    new ProgressSpinnerScenario(),
    new MarkdownScenario(),
    new DragDropScenario(),
    new NestedLayoutStressScenario(),
    new TerminalSuccessScenario(),
    new TerminalFailureScenario(),
};

var scenarioNames = scenarios.Select(s => s.Name).ToArray();

await Hex1bTerminal.CreateBuilder()
    .WithScrollback()
    .WithMouse()
    .WithHex1bFlow(async flow =>
    {
        while (!flow.CancellationToken.IsCancellationRequested)
        {
            // Step 1: Scenario picker
            IWidgetScenario? selectedScenario = null;

            var pickerStep = flow.Step(ctx => ctx.VStack(v =>
            [
                v.Text("╔══════════════════════════════════════╗"),
                v.Text("║   FlowWidgetCompatDemo               ║"),
                v.Text("║   Select a widget scenario to test   ║"),
                v.Text("╚══════════════════════════════════════╝"),
                v.Text(""),
                v.List(scenarioNames)
                    .OnItemActivated(e =>
                    {
                        selectedScenario = scenarios[e.ActivatedIndex];
                        ctx.Step.Complete(y => y.Text($"  ✓ Selected: {selectedScenario.Name}"));
                    })
                    .FixedHeight(scenarios.Length + 1),
            ]),
                options: opts => opts.MaxHeight = scenarios.Length + 7
            );

            await pickerStep.WaitForCompletionAsync(flow.CancellationToken);

            if (selectedScenario is null)
                break;

            // Step 2: Run selected scenario
            var currentScenario = selectedScenario;
            var scenarioStep = flow.Step(ctx =>
            {
                var scenarioWidget = currentScenario.Build(ctx);

                return ctx.VStack(v =>
                [
                    v.HStack(h =>
                    [
                        h.Text($" Testing: {currentScenario.Name}  "),
                        h.Text(" (Ctrl+Q to return)"),
                    ]),
                    v.Separator(),
                    scenarioWidget.FillHeight(),
                ]).WithInputBindings(b =>
                    b.Ctrl().Key(Hex1bKey.Q).Global().OverridesCapture().Action(() =>
                    {
                        ctx.Step.Complete(y => y.Text($"  ✓ Completed: {currentScenario.Name}"));
                    }, "Return to scenario picker")
                );
            },
                options: opts =>
                {
                    if (currentScenario.MaxHeight is { } maxHeight)
                        opts.MaxHeight = maxHeight;
                }
            );

            await scenarioStep.WaitForCompletionAsync(flow.CancellationToken);
        }
    })
    .Build()
    .RunAsync();
