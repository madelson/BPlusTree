using BenchmarkDotNet.Attributes;
using Medallion;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            Random random = Rand.CreateJavaRandom(seed: 654321);

            _insertionIndices = new (int, T)[Size];
            for (var i = 0; i < _insertionIndices.Length; ++i)
            {
                _insertionIndices[i] = (random.Next(i + 1), values[i]);
            }
        }

        [Benchmark]
        public object Insert_ImmutableList()
        {
            ImmutableList<T> immutableList = ImmutableList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                immutableList = immutableList.Insert(index, item);
            }
            return immutableList;
        }

        [Benchmark]
        public object Insert_NodeBasedImmutableList()
        {
            NodeBasedBPlusTreeImmutableList<T> nodeBasedImmutableList = NodeBasedBPlusTreeImmutableList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                nodeBasedImmutableList = nodeBasedImmutableList.Insert(index, item);
            }
            return nodeBasedImmutableList;
        }

        [Benchmark]
        public object Insert_ArrayBasedImmutableList()
        {
            ArrayBasedBPlusTreeImmutableList<T> arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList<T>.Empty;
            foreach (var (index, item) in _insertionIndices!)
            {
                arrayBasedImmutableList = arrayBasedImmutableList.Insert(index, item);
            }
            return arrayBasedImmutableList;
        }
    }
}
