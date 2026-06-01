using BenchmarkDotNet.Running;
using Guacamole.QueueProcessor.Benchmarks;

// Run with: dotnet run -c Release
// Or a specific benchmark: dotnet run -c Release -- --filter *Deserializer*

var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
