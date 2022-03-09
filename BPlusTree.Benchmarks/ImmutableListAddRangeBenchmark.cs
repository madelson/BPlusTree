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
    public class ImmutableListAddRangeBenchmark<T> where T : IComparable<T>
    {
        [Params(50, 10_000)]
        public int Size;

        [Params(50, 10_000)]
        public int AddedSize;

        private ImmutableList<T>? _immutableList;
        private NodeBasedBPlusTreeImmutableList<T>? _nodeBasedImmutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private T[]? _itemsArray;
        private IEnumerable<T>? _itemsEnumerable;

        [GlobalSetup]
        public void SetUp()
        {
            _immutableList = ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));
            _nodeBasedImmutableList = NodeBasedBPlusTreeImmutableList.CreateRange(_immutableList);
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(_immutableList);
            _itemsArray = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            _itemsEnumerable = _itemsArray.Select(t => t);
        }

        [Benchmark]
        public object AddRangeArray_ImmutableList() =>
            _immutableList!.AddRange(_itemsArray!);

        [Benchmark]
        public object AddRangeArray_NodeBasedImmutableList() =>
            _nodeBasedImmutableList!.AddRange(_itemsArray!);

        [Benchmark]
        public object AddRangeArray_ArrayBasedImmutableList() =>
            _arrayBasedImmutableList!.AddRange(_itemsArray!);

        [Benchmark]
        public object AddRangeEnumerable_ImmutableList() =>
            _immutableList!.AddRange(_itemsEnumerable!);

        [Benchmark]
        public object AddRangeEnumerable_NodeBasedImmutableList() =>
            _nodeBasedImmutableList!.AddRange(_itemsEnumerable!);

        [Benchmark]
        public object AddRangeEnumerable_ArrayBasedImmutableList() =>
            _arrayBasedImmutableList!.AddRange(_itemsEnumerable!);
    }
}
