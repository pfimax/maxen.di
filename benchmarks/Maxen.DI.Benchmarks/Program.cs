using BenchmarkDotNet.Running;
using Maxen.DI.Benchmarks;

// Run with:  dotnet run -c Release --project Maxen.DI.Benchmarks -- --filter "*"
BenchmarkSwitcher.FromAssembly(typeof(ResolveBenchmarks).Assembly).Run(args);
