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
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            _immutableList = System.Collections.Immutable.ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark(Baseline = true)]
        public T ImmutableList()
        {
            T min = _immutableList![0];
            for (var i = 1; i < Size; ++i)
            {
                T value = _immutableList[i];
                if (value.CompareTo(min) < 0) { min = value; }
            }
            return min;
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public T ArrayBasedImmutableList()
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
