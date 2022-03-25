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
    public class ImmutableListCopyToBenchmark<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private ImmutableList<T>? _immutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private ImmutableTreeList<T>? _tunnelVisionImmutableList;
        private T[]? _array;

        [GlobalSetup(Target = nameof(ImmutableList))]
        public void SetUpImmutableList() =>
            _array = new T[(_immutableList = System.Collections.Immutable.ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size))).Count];

        [Benchmark]
        public void ImmutableList() => _immutableList!.CopyTo(_array!, 0);

        [GlobalSetup(Target = nameof(ArrayBasedImmutableList))]
        public void SetUpArrayBasedImmutableList() =>
            _array = new T[(_arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size))).Count];

        [Benchmark]
        public void ArrayBasedImmutableList() => _arrayBasedImmutableList!.CopyTo(_array!, 0);

        [GlobalSetup(Target = nameof(TunnelVisionImmutableList))]
        public void SetUpTunnelVisionImmutableList() =>
           _array = new T[(_tunnelVisionImmutableList = ImmutableTreeList.CreateRange(ValuesGenerator.UniqueValues<T>(Size))).Count];

        [Benchmark]
        public void TunnelVisionImmutableList() => _tunnelVisionImmutableList!.CopyTo(_array!, 0);
    }
}
