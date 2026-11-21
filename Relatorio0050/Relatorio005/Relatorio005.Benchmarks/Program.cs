using BenchmarkDotNet.Running;
using Relatorio005.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(BuscaBenchmarks).Assembly).Run(args);
