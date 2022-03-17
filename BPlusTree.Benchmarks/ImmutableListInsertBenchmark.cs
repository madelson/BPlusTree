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
    public class ImmutableListInsertBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private (int Index, T Item)[]? _insertionIndices;

        [GlobalSetup]
        public void SetUp()
        {
            T[] values = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            var random = new Random(654321);

            _insertionIndices = new (int, T)[Size];
            for (var i = 0; i < _insertionIndices.Length; ++i)
            {
                _insertionIndices[i] = (random.Next(i + 1), values[i]);
            }
        }

        [Benchmark(Baseline = true)]
        public object ImmutableList()
        {
            ImmutableList<T> immutableList = ImmutableList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                immutableList = immutableList.Insert(index, item);
            }
            return immutableList;
        }

        [Benchmark]
        public object ArrayBasedImmutableList()
        {
            ArrayBasedBPlusTreeImmutableList<T> arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                arrayBasedImmutableList = arrayBasedImmutableList.Insert(index, item);
            }
            return arrayBasedImmutableList;
        }

        [Benchmark]
        public object TunnelVisionImmutableList()
        {
            ImmutableTreeList<T> tunnelVisionImmutableList = ImmutableTreeList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                tunnelVisionImmutableList = tunnelVisionImmutableList.Insert(index, item);
            }
            return tunnelVisionImmutableList;
        }
    }
}
