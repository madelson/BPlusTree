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
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private IEnumerable<T>? _items;

        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            T[] allItems = ValuesGenerator.UniqueValues<T>(Size + AddedSize).ToArray();
            IEnumerable<T> items = allItems.Take(AddedSize);
            _items = Array ? items.ToArray() : items;

            listField = createRange(allItems.Skip(AddedSize));
        }

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            SetUpHelper(ref _immutableList, System.Collections.Immutable.ImmutableList.CreateRange<T>);

        [Benchmark(Baseline = true)]
        public object ImmutableList() =>
            _immutableList!.AddRange(_items!);

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public object ArrayBasedImmutableList() =>
            _arrayBasedImmutableList!.AddRange(_items!);

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() =>
            SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public object TunnelVisionImmutableList() =>
            _tunnelVisionImmutableList!.AddRange(_items!);
    }
}
