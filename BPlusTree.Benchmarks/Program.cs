﻿using BenchmarkDotNet.Running;
using BPlusTree.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .RunAll();

//while (true)
//{
//    foreach (var size in new[] { 5, 50, 512, 10_000 })
//    {
//        var b = new ImmutableListInsertBenchmark<string> { Size = size };
//        b.SetUp();
//        b.Insert_ImmutableList();
//        b.Insert_ArrayBasedImmutableList();
//        b.Insert_NodeBasedImmutableList();
//    }
//}

//BenchmarkRunner.Run(typeof(ImmutableListInsertBenchmark<string>));

//args = new[] { "--filter", "*", "--cli", @"C:\Program Files\dotnet\dotnet.exe" };

//BenchmarkSwitcher
//    .FromAssembly(typeof(Program).Assembly)
//    .RunAllJoined();
//.Run(args);
//.Run(args, new FastAndDirtyConfig());