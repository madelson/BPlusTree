using BenchmarkDotNet.Attributes;
using Medallion;
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
    public class ImmutableListAddBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        private T[]? _values;

        [GlobalSetup]
        public void SetUp()
        {
            _values = ValuesGenerator.UniqueValues<T>(Size).ToArray();
        }

        [Benchmark(Baseline = true)]
        public object Insert_ImmutableList()
        {
            ImmutableList<T> immutableList = ImmutableList<T>.Empty;
            foreach (T value in _values!)
            {
                immutableList = immutableList.Add(value);
            }
            return immutableList;
        }

        // not implemented
        //[Benchmark]
        //public object Insert_NodeBasedImmutableList()
        //{
        //    NodeBasedBPlusTreeImmutableList<T> bPlusTreeImmutableList = NodeBasedBPlusTreeImmutableList<T>.Empty;
        //    foreach (T value in _values!)
        //    {
        //        bPlusTreeImmutableList = bPlusTreeImmutableList.Add(value);
        //    }
        //    return bPlusTreeImmutableList;
        //}

        [Benchmark]
        public object Insert_ArrayBasedImmutableList()
        {
            ArrayBasedBPlusTreeImmutableList<T> arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList<T>.Empty;
            foreach (T value in _values!)
            {
                arrayBasedImmutableList = arrayBasedImmutableList.Add(value);
            }
            return arrayBasedImmutableList;
        }
    }
}
