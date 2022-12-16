using BenchmarkDotNet.Running;
using BPlusTree.Benchmarks;

//BenchmarkRunner.Run(typeof(ImmutableListSetItemBenchmark<int>));
//BenchmarkRunner.Run(typeof(ImmutableListSetItemBenchmark<string>));
//BenchmarkRunner.Run(typeof(ImmutableListBuilderSetItemBenchmark<int>));
//BenchmarkRunner.Run(typeof(ImmutableListBuilderSetItemBenchmark<string>));
BenchmarkRunner.Run(typeof(ImmutableListBuilderAddBenchmark<int>));
BenchmarkRunner.Run(typeof(ImmutableListBuilderAddBenchmark<string>));

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .RunAll();