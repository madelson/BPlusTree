using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using InternalEntry = ArrayBasedBPlusTreeImmutableListInternalEntry;

    public sealed class ArrayBasedBPlusTreeImmutableList<T>
    {
        public static ArrayBasedBPlusTreeImmutableList<T> Empty { get; } = new(Array.Empty<LeafEntry>(), 0);

        private readonly Array _root;
        private readonly int _count;

        private ArrayBasedBPlusTreeImmutableList(Array root, int count)
        {
            _root = root;
            _count = count;
        }

        public int Count => _count;

        public T this[int index] => this.ItemRef(index);

        public ref readonly T ItemRef(int index)
        {
            if ((uint)index >= (uint)this._count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            Array current = _root;
            while (true)
            {
                if (current.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(current);

                    for (var i = 0; i < internalNode.Length; ++i)
                    {
                        if (index < internalNode[i].CumulativeChildCount)
                        {
                            if (i != 0)
                            {
                                index -= internalNode[i - 1].CumulativeChildCount;
                            }
                            current = internalNode[i].Child;
                            break;
                        }
                    }
                    Debug.Assert(current != internalNode);
                }
                else
                {
                    return ref Unsafe.As<LeafEntry[]>(current)[index].Item;
                }
            }
        }

        // todo remove
        public static ArrayBasedBPlusTreeImmutableList<T> CreateRange(T[] items)
        {
            var leafSize = Unsafe.SizeOf<T>() == 1 ? 128
                : Unsafe.SizeOf<T>() == 2 ? 64
                : Unsafe.SizeOf<T>() == 4 ? 32
                : Unsafe.SizeOf<T>() == 8 ? 16
                : 8;
            var internalSize = Math.Max(leafSize / 2, 8);

            var leafQueue = new Queue<LeafEntry[]>();
            var leafBuilder = new List<LeafEntry>();
            for (var i = 0; i < items.Length; ++i)
            {
                leafBuilder.Add(new() { Item = items[i] });
                if (leafBuilder.Count == leafSize)
                {
                    leafQueue.Enqueue(leafBuilder.ToArray());
                    leafBuilder.Clear();
                }
            }
            if (leafBuilder.Count > 0)
            {
                leafQueue.Enqueue(leafBuilder.ToArray());
            }
                 
            if (leafQueue.Count == 1) { return new(leafQueue.Single(), leafQueue.Single().Length); }

            var internalQueue = new Queue<InternalEntry[]>();
            var internalBuilder = new List<InternalEntry>();
            var builderCount = 0;
            while (leafQueue.Count > 0)
            {
                var leaf = leafQueue.Dequeue();
                internalBuilder.Add(new() { CumulativeChildCount = builderCount += leaf.Length, Child = leaf });
                if (internalBuilder.Count == internalSize)
                {
                    internalQueue.Enqueue(internalBuilder.ToArray());
                    internalBuilder.Clear();
                    builderCount = 0;
                }
            }
            if (internalBuilder.Count > 0)
            {
                internalQueue.Enqueue(internalBuilder.ToArray());
                internalBuilder.Clear();
                builderCount = 0;
            }

            while (internalQueue.Count > 1)
            {
                var newInternalQueue = new Queue<InternalEntry[]>();
                while (internalQueue.Count > 0)
                {
                    var node = internalQueue.Dequeue();
                    internalBuilder.Add(new() { CumulativeChildCount = builderCount += node[node.Length - 1].CumulativeChildCount, Child = node });
                    if (internalBuilder.Count == internalSize)
                    {
                        newInternalQueue.Enqueue(internalBuilder.ToArray());
                        internalBuilder.Clear();
                        builderCount = 0;
                    }
                }
                if (internalBuilder.Count > 0)
                {
                    newInternalQueue.Enqueue(internalBuilder.ToArray());
                    internalBuilder.Clear();
                    builderCount = 0;
                }
                internalQueue = newInternalQueue;
            }

            var root = internalQueue.Single();
            Debug.Assert(root[root.Length - 1].CumulativeChildCount == items.Length);
            return new(root, root[root.Length - 1].CumulativeChildCount);
        }

        private struct LeafEntry { public T Item; }
    }
}
