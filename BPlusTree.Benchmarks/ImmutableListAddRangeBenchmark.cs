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
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private IEnumerable<T>? _items;

        [GlobalSetup]
        public void SetUp()
        {            
            T[] items = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            _items = Array ? items : items.Select(t => t);
        }

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            _immutableList = System.Collections.Immutable.ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark(Baseline = true)]
        public object ImmutableList() =>
            _immutableList!.AddRange(_items!);

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public object ArrayBasedImmutableList() =>
            _arrayBasedImmutableList!.AddRange(_items!);
    }
}
