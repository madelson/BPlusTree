using BenchmarkDotNet.Running;
using BPlusTree.Benchmarks;

BenchmarkRunner.Run(typeof(ImmutableListSetItemBenchmark<int>));
BenchmarkRunner.Run(typeof(ImmutableListSetItemBenchmark<string>));

//BenchmarkSwitcher
//    .FromAssembly(typeof(Program).Assembly)
//    .RunAll();