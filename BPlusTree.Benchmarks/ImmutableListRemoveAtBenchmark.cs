using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TunnelVisionLabs.Collections.Trees.Immutable;

namespace BPlusTree.Benchmarks
{
    [GenericTypeArguments(typeof(int))]
    [GenericTypeArguments(typeof(string))]
    [MemoryDiagnoser]
    [DisassemblyDiagnoser]
    public class ImmutableListRemoveAtBenchmark<T> : IComparisonBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private int[] _removes;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() => SetUpHelper(ref _immutableList, System.Collections.Immutable.ImmutableList.CreateRange<T>);

        [Benchmark(Baseline = true)]
        public ImmutableList<T> ImmutableList()
        {
            var immutableList = _immutableList!;
            foreach (var index in _removes)
            {
                immutableList = immutableList.RemoveAt(index);
            }
            return immutableList;
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() => SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList()
        {
            var immutableList = _arrayBasedImmutableList!;
            foreach (var index in _removes)
            {
                immutableList = immutableList.RemoveAt(index);
            }
            return immutableList;
        }

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() => SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList()
        {
            var immutableList = _tunnelVisionImmutableList!;
            foreach (var index in _removes)
            {
                immutableList = immutableList.RemoveAt(index);
            }
            return immutableList;
        }

        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            var items = ValuesGenerator.UniqueValues<T>(2 * Size);
            listField = createRange(items.Take(Size));

            var random = new Random(12345);
            var removes = new List<int>();
            for (var i = Size; i > 0; --i)
            {
                removes.Add(random.Next(i));
            }
            _removes = removes.ToArray();
        }
    }
}
