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
    public class ImmutableListBuilderAddBenchmark<T> : IComparisonBenchmark<T> where T : IComparable<T>
    {
        [Params(15_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private T[]? _items;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() => SetUpHelper(ref _immutableList, System.Collections.Immutable.ImmutableList.CreateRange<T>);

        [Benchmark(Baseline = true)]
        public ImmutableList<T> ImmutableList()
        {
            var builder = _immutableList!.ToBuilder();
            foreach (var item in _items!)
            {
                builder.Add(item);
            }
            return builder.ToImmutable();
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() => SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList()
        {
            var builder = _arrayBasedImmutableList!.ToBuilder();
            foreach (var item in _items!)
            {
                builder.Add(item);
            }
            return builder.ToImmutable();
        }

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() => SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList()
        {
            var builder = _tunnelVisionImmutableList!.ToBuilder();
            foreach (var item in _items!)
            {
                builder.Add(item);
            }
            return builder.ToImmutable();
        }

        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            _items = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            listField = createRange(Enumerable.Empty<T>());
        }
    }
}
