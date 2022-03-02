using BenchmarkDotNet.Running;
using BPlusTree.Benchmarks;

args = new[] { "--filter", "*", "--cli", @"C:\Program Files\dotnet\dotnet.exe" };

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, new FastAndDirtyConfig());