using Hex1b;
using KgpValidation;

await using var workload = new KgpValidationWorkload();
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithDiagnostics("KgpValidation", forceEnable: true)
    .Build();

try
{
    return await terminal.RunAsync();
}
catch (OperationCanceledException)
{
    return 0;
}
