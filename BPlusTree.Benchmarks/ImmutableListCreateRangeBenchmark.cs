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
    public class ImmutableListCreateRangeBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private T[]? _itemsArray;
        private IEnumerable<T>? _itemsEnumerable;

        [GlobalSetup]
        public void SetUp()
        {
            _itemsArray = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            _itemsEnumerable = _itemsArray.Select(t => t);
        }

        [Benchmark]
        public object CreateRangeFromArray_ImmutableList() =>
            ImmutableList.CreateRange(_itemsArray!);

        [Benchmark]
        public object CreateRangeFromArray_NodeBasedImmutableList() =>
            NodeBasedBPlusTreeImmutableList.CreateRange(_itemsArray!);

        [Benchmark]
        public object CreateRangeFromArray_ArrayBasedImmutableList() =>
            ArrayBasedBPlusTreeImmutableList.CreateRange(_itemsArray!);

        [Benchmark]
        public object CreateRangeFromEnumerable_ImmutableList() =>
            ImmutableList.CreateRange(_itemsEnumerable!);

        [Benchmark]
        public object CreateRangeFromEnumerable_NodeBasedImmutableList() =>
            NodeBasedBPlusTreeImmutableList.CreateRange(_itemsEnumerable!);

        [Benchmark]
        public object CreateRangeFromEnumerable_ArrayBasedImmutableList() =>
            ArrayBasedBPlusTreeImmutableList.CreateRange(_itemsEnumerable!);
    }
}
