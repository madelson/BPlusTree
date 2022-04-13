using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TunnelVisionLabs.Collections.Trees.Immutable;

namespace BPlusTree.Benchmarks
{
    [GenericTypeArguments(typeof(int))]
    [GenericTypeArguments(typeof(string))]
    [MemoryDiagnoser]
    public class ImmutableListIndexOfBenchmark<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            _immutableList = System.Collections.Immutable.ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark(Baseline = true)]
        public int ImmutableList() => _immutableList!.IndexOf(default!, 0, Size, null);

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public int ArrayBasedImmutableList() => _arrayBasedImmutableList!.IndexOf(default!, 0, Size, null);

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() =>
           _tunnelVisionImmutableList = ImmutableTreeList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public int TunnelVisionImmutableList() => _tunnelVisionImmutableList!.IndexOf(default!, 0, Size, null);
    }
}
