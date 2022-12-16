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
    public class ImmutableListEnumerationBenchmark<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            _immutableList = System.Collections.Immutable.ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark(Baseline = true)]
        public int ImmutableList()
        {
            var sum = 0;
            foreach (var item in _immutableList!)
            {
                sum += EqualityComparer<T>.Default.Equals(item, default) ? -1 : 1;
            }
            return sum;
        }

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public int ArrayBasedImmutableList()
        {
            var sum = 0;
            foreach (var item in _arrayBasedImmutableList!)
            {
                sum += EqualityComparer<T>.Default.Equals(item, default) ? -1 : 1;
            }
            return sum;
        }

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() =>
           _tunnelVisionImmutableList = ImmutableTreeList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));

        [Benchmark]
        public int TunnelVisionImmutableList()
        {
            var sum = 0;
            foreach (var item in _tunnelVisionImmutableList!)
            {
                sum += EqualityComparer<T>.Default.Equals(item, default) ? -1 : 1;
            }
            return sum;
        }
    }
}
