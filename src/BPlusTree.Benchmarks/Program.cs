using BenchmarkDotNet.Running;
using Medallion.Collections;
using Medallion.Collections.Benchmarks;

//var tr = BPlusTreeImmutableList.CreateRange(Enumerable.Range(0, 10000));
//var td = Enumerable.Range(0, 50).ToArray();
//while (true)
//{
//    tr.AddRange(td);
//}

BenchmarkRunner.Run(typeof(AddRangeBenchmark<int>));
BenchmarkRunner.Run(typeof(AddRangeBenchmark<string>));