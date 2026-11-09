using System.Globalization;

if (args.Length != 2 || args[0] != "stdin-probe")
    return 2;

var bytes = new byte[int.Parse(args[1], CultureInfo.InvariantCulture)];
Console.WriteLine("STDIN_PROBE_READY");
await Console.OpenStandardInput().ReadExactlyAsync(bytes);
Console.WriteLine("STDIN_BYTES=" + Convert.ToHexString(bytes));
Console.WriteLine("STDIN_PROBE_DONE");
return 0;
