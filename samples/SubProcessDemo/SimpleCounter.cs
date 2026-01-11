// A simple counter that outputs to stdout - used to test WithProcess
// Exits after 20 seconds
var counter = 0;

while (counter < 20)
{
    Console.WriteLine($"Iteration {counter}");
    counter++;
    await Task.Delay(1000);
}

Console.WriteLine("Counter finished.");
