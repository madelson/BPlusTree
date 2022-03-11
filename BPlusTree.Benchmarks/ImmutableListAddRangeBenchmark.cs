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

        [Params(false, true)]
        public bool Array;

        private ImmutableList<T>? _immutableList;
        //private NodeBasedBPlusTreeImmutableList<T>? _nodeBasedImmutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private IEnumerable<T>? _items;

        [GlobalSetup]
        public void SetUp()
        {
            _immutableList = ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));
            //_nodeBasedImmutableList = NodeBasedBPlusTreeImmutableList.CreateRange(_immutableList);
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(_immutableList);
            
            T[] items = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            _items = Array ? items : items.Select(t => t);
        }

        [Benchmark(Baseline = true)]
        public object AddRange_ImmutableList() =>
            _immutableList!.AddRange(_items!);

        //[Benchmark]
        //public object AddRangeArray_NodeBasedImmutableList() =>
        //    _nodeBasedImmutableList!.AddRange(_itemsArray!);

        [Benchmark]
        public object AddRange_ArrayBasedImmutableList() =>
            _arrayBasedImmutableList!.AddRange(_items!);
    }
}
