using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TunnelVisionLabs.Collections.Trees.Immutable;

namespace BPlusTree.Benchmarks
{
    [GenericTypeArguments(typeof(int))]
    [GenericTypeArguments(typeof(string))]
    [MemoryDiagnoser]
    [DisassemblyDiagnoser]
    public class ImmutableListSetItemBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private (int Index, T Item)[] _sets;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() => SetUpHelper(ref _immutableList, System.Collections.Immutable.ImmutableList.CreateRange<T>);

        [Benchmark(Baseline = true)]
        public object ImmutableList()
        {
            var immutableList = _immutableList!;
            foreach (var (index, item) in _sets)
            {
                immutableList = immutableList.SetItem(index, item);
            }
            return immutableList;
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() => SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public object ArrayBasedImmutableList()
        {
            var immutableList = _arrayBasedImmutableList!;
            foreach (var (index, item) in _sets)
            {
                immutableList = immutableList.SetItem(index, item);
            }
            return immutableList;
        }

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() => SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public object TunnelVisionImmutableList()
        {
            var immutableList = _tunnelVisionImmutableList!;
            foreach (var (index, item) in _sets)
            {
                immutableList = immutableList.SetItem(index, item);
            }
            return immutableList;
        }


        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            var items = ValuesGenerator.UniqueValues<T>(2 * Size);
            listField = createRange(items.Take(Size));

            var random = new Random(12345);
            _sets = items.Skip(Size).OrderBy(_ => random.Next())
                .Select((item, index) => (index, item))
                .ToArray();
        }
    }
}
