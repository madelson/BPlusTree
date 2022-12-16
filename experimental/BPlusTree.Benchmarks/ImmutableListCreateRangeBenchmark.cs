using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TunnelVisionLabs.Collections.Trees.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Benchmarks
{
    [GenericTypeArguments(typeof(int))]
    [GenericTypeArguments(typeof(string))]
    [MemoryDiagnoser]
    [DisassemblyDiagnoser]
    public class ImmutableListCreateRangeBenchmark<T> : IComparisonBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        [Params(false, true)]
        public bool Array;

        private IEnumerable<T>? _items;

        [GlobalSetup]
        public void SetUp()
        {
            T[] items = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            _items = Array ? items : items.Select(t => t);
        }

        [Benchmark(Baseline = true)]
        public ImmutableList<T> ImmutableList() =>
            System.Collections.Immutable.ImmutableList.CreateRange(_items!);

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList() =>
            ArrayBasedBPlusTreeImmutableList.CreateRange(_items!);

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList() =>
            ImmutableTreeList.CreateRange(_items!);
    }
}
