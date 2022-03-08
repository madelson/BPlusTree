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
    public class ImmutableListInsertBenchmark<T> where T : IComparable<T>
    {
        [Params(5, 50, 512, 10_000)]
        public int Size;

        /// <summary>
        /// <see cref="BPlusTreeImmutableList.CreateRange{T}(IEnumerable{T})"/> creates a more compat tree
        /// which is less wasteful in terms of memory but initially more costly for insertions in the middle.
        /// </summary>
        [Params(false, true)]
        public bool Compacted;

        private ImmutableList<T>? _immutableList;
        private BPlusTreeImmutableList<T>? _bPlusTreeImmutableList;
        private ArrayBasedBPlusTreeImmutableList<T>? _arrayBasedImmutableList;
        private (int Index, T Item)[]? _insertionIndices;

        [GlobalSetup]
        public void SetUp()
        {
            T[] values = ValuesGenerator.UniqueValues<T>(Size).ToArray();
            Random random = Rand.CreateJavaRandom(seed: 654321);

            if (Compacted)
            {
                _immutableList = ImmutableList.CreateRange(ValuesGenerator.UniqueValues<T>(Size));
                _bPlusTreeImmutableList = BPlusTreeImmutableList.CreateRange(_immutableList);
                _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList.CreateRange(_immutableList.ToArray());
            }
            else
            {
                _immutableList = ImmutableList<T>.Empty;
                _bPlusTreeImmutableList = BPlusTreeImmutableList<T>.Empty;
                _arrayBasedImmutableList = ArrayBasedBPlusTreeImmutableList<T>.Empty;
                for (var i = 0; i < Size; ++i)
                {
                    var index = random.Next(i + 1);
                    _immutableList = _immutableList.Insert(index, values[i]);
                    _bPlusTreeImmutableList = _bPlusTreeImmutableList.Insert(index, values[i]);
                    _arrayBasedImmutableList = _arrayBasedImmutableList.Insert(index, values[i]);
                }
            }

            _insertionIndices = new (int, T)[Size];
            for (var i = 0; i < _insertionIndices.Length; ++i)
            {
                _insertionIndices[i] = (random.Next(Size + i), values[i]);
            }
        }

        [Benchmark]
        public object Insert_ImmutableList()
        {
            ImmutableList<T> immutableList = _immutableList!;
            foreach (var (index, item) in _insertionIndices!)
            {
                immutableList = immutableList.Insert(index, item);
            }
            return immutableList;
        }

        [Benchmark]
        public object Insert_BPlusTreeImmutableList()
        {
            BPlusTreeImmutableList<T> bPlusTreeImmutableList = _bPlusTreeImmutableList!;
            foreach (var (index, item) in _insertionIndices!)
            {
                bPlusTreeImmutableList = bPlusTreeImmutableList.Insert(index, item);
            }
            return bPlusTreeImmutableList;
        }

        [Benchmark]
        public object Insert_ArrayBasedImmutableList()
        {
            ArrayBasedBPlusTreeImmutableList<T> arrayBasedImmutableList = _arrayBasedImmutableList!;
            foreach (var (index, item) in _insertionIndices!)
            {
                arrayBasedImmutableList = arrayBasedImmutableList.Insert(index, item);
            }
            return arrayBasedImmutableList;
        }
    }
}
