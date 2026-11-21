using BenchmarkDotNet.Running;

// Executar SEMPRE em Release:
//   dotnet run -c Release --project Benchmarks -- --filter "*"
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
