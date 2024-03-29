﻿using BenchmarkDotNet.Attributes;
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
    public class ImmutableListAddBenchmark<T> : IComparisonBenchmark<T> where T : IComparable<T>
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
        public ImmutableList<T> ImmutableList()
        {
            ImmutableList<T> immutableList = ImmutableList<T>.Empty;
            foreach (T value in _values!)
            {
                immutableList = immutableList.Add(value);
            }
            return immutableList;
        }

        [Benchmark]
        public ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList()
        {
            ArrayBasedBPlusTreeImmutableList<T> arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList<T>.Empty;
            foreach (T value in _values!)
            {
                arrayBasedImmutableList = arrayBasedImmutableList.Add(value);
            }
            return arrayBasedImmutableList;
        }

        [Benchmark]
        public ImmutableTreeList<T> TunnelVisionImmutableList()
        {
            ImmutableTreeList<T> tunnelVisionImmutableList = ImmutableTreeList<T>.Empty;
            foreach (T value in _values!)
            {
                tunnelVisionImmutableList = tunnelVisionImmutableList.Add(value);
            }
            return tunnelVisionImmutableList;
        }
    }
}
