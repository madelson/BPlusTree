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
    public class ImmutableListRemoveRangeBenchmark<T> : IComparisonBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private int _index, _count;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() => SetUpHelper(ref _immutableList, System.Collections.Immutable.ImmutableList.CreateRange<T>);

        [Benchmark(Baseline = true)]
        public ImmutableList<T> ImmutableList() => _immutableList!.RemoveRange(_index, _count);

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() => SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList() => _arrayBasedImmutableList!.RemoveRange(_index, _count);

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() => SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList() => _tunnelVisionImmutableList!.RemoveRange(_index, _count);

        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            var items = ValuesGenerator.UniqueValues<T>(2 * Size);
            listField = createRange(items.Take(Size));

            _index = Size / 3;
            _count = 2 * _index;
        }
    }
}
