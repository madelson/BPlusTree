using BenchmarkDotNet.Attributes;
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
    public class ImmutableListIndexBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private BPlusTreeImmutableList<T>? _bPlusTreeImmutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;

        [GlobalSetup]
        public void SetUp()
        {
            _immutableList = ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));
            _bPlusTreeImmutableList = BPlusTreeImmutableList.CreateRange(_immutableList);
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(_immutableList.ToArray());
        }

        [Benchmark]
        public T IndexerIteration_ImmutableList()
        {
            T min = _immutableList![0];
            for (var i = 1; i < Size; ++i)
            {
                T value = _immutableList[i];
                if (value.CompareTo(min) < 0) { min = value; }
            }
            return min;
        }

        [Benchmark]
        public T IndexerIteration_BPlusTreeImmutableList()
        {
            T min = _bPlusTreeImmutableList![0];
            for (var i = 1; i < Size; ++i)
            {
                T value = _bPlusTreeImmutableList[i];
                if (value.CompareTo(min) < 0) { min = value; }
            }
            return min;
        }

        [Benchmark]
        public T IndexerIteration_ArrayBasedImmutableList()
        {
            T min = _arrayBasedImmutableList![0];
            for (var i = 1; i < Size; ++i)
            {
                T value = _arrayBasedImmutableList[i];
                if (value.CompareTo(min) < 0) { min = value; }
            }
            return min;
        }
    }
}
