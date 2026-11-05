using Maxen.DI.Samples.Features;

// One small, runnable example per feature. Run one by name, or all of them:
//   dotnet run -- lifetimes
//   dotnet run -- all
var samples = SampleCatalog.All();

if (args.Length == 0)
{
    Console.WriteLine("Pick a sample (or 'all'):");
    foreach (var sample in samples)
        Console.WriteLine($"  {sample.Name,-14} {sample.Summary}");
    return 0;
}

var wanted = args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
    ? samples
    : samples.Where(s => s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();

if (wanted.Count == 0)
{
    Console.Error.WriteLine($"No sample called '{args[0]}'. Run without arguments to see the list.");
    return 2;
}

foreach (var sample in wanted)
{
    Console.WriteLine();
    Console.WriteLine($"=== {sample.Name}: {sample.Summary}");
    await sample.RunAsync();
}

return 0;
