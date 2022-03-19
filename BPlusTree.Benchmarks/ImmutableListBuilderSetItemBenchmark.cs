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
    public class ImmutableListBuilderSetItemBenchmark<T> where T : IComparable<T>
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
        public ImmutableList<T> ImmutableList()
        {
            var builder = _immutableList!.ToBuilder();
            foreach (var (index, item) in _sets)
            {
                builder[index] = item;
            }
            return builder.ToImmutable();
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() => SetUpHelper(ref _arrayBasedImmutableList, ArrayBasedBPlusTreeImmutableList.CreateRange<T>);

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList()
        {
            var builder = _arrayBasedImmutableList!.ToBuilder();
            foreach (var (index, item) in _sets)
            {
                builder[index] = item;
            }
            return builder.ToImmutable();
        }

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() => SetUpHelper(ref _tunnelVisionImmutableList, ImmutableTreeList.CreateRange<T>);

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList()
        {
            var builder = _tunnelVisionImmutableList!.ToBuilder();
            foreach (var (index, item) in _sets)
            {
                builder[index] = item;
            }
            return builder.ToImmutable();
        }

        private void SetUpHelper<TList>(ref TList listField, Func<IEnumerable<T>, TList> createRange)
        {
            var items = ValuesGenerator.UniqueValues<T>((int)(1.25 * Size));
            listField = createRange(items.Take(Size));

            var random = new Random(12345);
            _sets = items.Skip(Size)
                .Select((item, index) => (index, item))
                .OrderBy(_ => random.Next())
                .ToArray();
        }
    }
}
