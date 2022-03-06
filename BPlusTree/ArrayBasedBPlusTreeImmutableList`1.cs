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

    public sealed class ArrayBasedBPlusTreeImmutableList<T> : IReadOnlyList<T>
    {
        public static ArrayBasedBPlusTreeImmutableList<T> Empty { get; } = new(Array.Empty<LeafEntry>(), 0);

        private readonly Array _root;
        private readonly int _count;

        private ArrayBasedBPlusTreeImmutableList(Array root, int count)
        {
            _root = root;
            _count = count;
            AssertValid();
        }

        public int Count => _count;

        public T this[int index] => ItemRef(index);

        public ref readonly T ItemRef(int index)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

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

        public ArrayBasedBPlusTreeImmutableList<T> Insert(int index, T item)
        {
            var count = _count;
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            Array inserted = Insert(_root, index, item, out Array? split);
            return split is null
                ? new(inserted, count + 1)
                : new(
                    new InternalEntry[]
                    {
                        new() { Child = inserted, CumulativeChildCount = GetCount(inserted) },
                        new() { Child = split, CumulativeChildCount = count + 1 },
                    },
                    count + 1
                );
        }

        private static Array Insert(Array node, int index, T item, out Array? split)
        {
            Debug.Assert(index >= 0 && index <= GetCount(node));

            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                var childIndex = 0;
                while (childIndex < internalNode.Length - 1 && internalNode[childIndex].CumulativeChildCount <= index) { ++childIndex; }

                Array updatedChild = Insert(
                    internalNode[childIndex].Child,
                    childIndex == 0 ? index : index - internalNode[childIndex - 1].CumulativeChildCount,
                    item,
                    out Array? splitChild);

                // case 1: update
                if (splitChild is null)
                {
                    var updated = new InternalEntry[internalNode.Length];
                    internalNode.AsSpan().CopyTo(updated);
                    updated[childIndex].Child = updatedChild;
                    for (var i = childIndex; i < updated.Length; ++i)
                    {
                        ++updated[i].CumulativeChildCount;
                    }
                    split = null;
                    return updated;
                }

                // case 2: expand
                if (internalNode.Length < MaxInternalNodeSize)
                {
                    var expanded = new InternalEntry[internalNode.Length + 1];
                    internalNode.AsSpan(0, childIndex).CopyTo(expanded);
                    expanded[childIndex] = new()
                    {
                        Child = updatedChild,
                        CumulativeChildCount = childIndex == 0 
                            ? GetCount(updatedChild)
                            : internalNode[childIndex - 1].CumulativeChildCount + GetCount(updatedChild),
                    };
                    expanded[childIndex + 1] = new() 
                    { 
                        Child = splitChild, 
                        CumulativeChildCount = internalNode[childIndex].CumulativeChildCount + 1 
                    };
                    internalNode.AsSpan(childIndex + 1).CopyTo(expanded.AsSpan(childIndex + 2));
                    for (var i = childIndex + 2; i < expanded.Length; ++i)
                    {
                        ++expanded[i].CumulativeChildCount;
                    }
                    split = null;
                    return expanded;
                }

                // case 3: split
                var left = new InternalEntry[LeftInternalNodeSplitSize];
                var right = new InternalEntry[MaxInternalNodeSize + 1 - LeftInternalNodeSplitSize];
                var countAdjustment = 0;
                for (var i = 0; i <= internalNode.Length; i++)
                {
                    ref InternalEntry location = ref (i < LeftInternalNodeSplitSize ? ref left[i] : ref right[i - LeftInternalNodeSplitSize]);
                    if (i < childIndex)
                    {
                        location = internalNode[i];
                        location.CumulativeChildCount -= countAdjustment;
                    }
                    else if (i == childIndex)
                    {
                        location = new()
                        {
                            Child = updatedChild,
                            CumulativeChildCount = childIndex == 0
                            ? GetCount(updatedChild)
                            : internalNode[childIndex - 1].CumulativeChildCount + GetCount(updatedChild) - countAdjustment,
                        };
                    }
                    else if (i == childIndex + 1)
                    {
                        location = new()
                        {
                            Child = splitChild,
                            CumulativeChildCount = internalNode[childIndex].CumulativeChildCount + 1 - countAdjustment
                        };
                    }
                    else // i > childIndex + 1
                    {
                        location = internalNode[i - 1];
                        location.CumulativeChildCount += 1 - countAdjustment;
                    }

                    if (i == LeftInternalNodeSplitSize - 1)
                    {
                        countAdjustment = location.CumulativeChildCount;
                    }
                }
                split = right;
                return left;
            }

            LeafEntry[] leafNode = Unsafe.As<LeafEntry[]>(node);

            // case 1: expand
            if (leafNode.Length < MaxLeafNodeSize)
            {
                var expandedLeaf = new LeafEntry[leafNode.Length + 1];
                leafNode.AsSpan(0, index).CopyTo(expandedLeaf);
                expandedLeaf[index].Item = item;
                leafNode.AsSpan(index).CopyTo(expandedLeaf.AsSpan(index + 1));
                split = null;
                return expandedLeaf;
            }

            // case 2: split
            var leftLeaf = new LeafEntry[LeftLeafNodeSplitSize];
            var rightLeaf = new LeafEntry[MaxLeafNodeSize + 1 - LeftLeafNodeSplitSize];
            var insertionOffset = 0;
            for (var i = 0; i <= leafNode.Length; i++)
            {
                if (i == index)
                {
                    insertionOffset = 1;
                    (i < LeftLeafNodeSplitSize ? ref leftLeaf[i] : ref rightLeaf[i - LeftLeafNodeSplitSize]).Item = item;
                }
                else
                {
                    (i < LeftLeafNodeSplitSize ? ref leftLeaf[i] : ref rightLeaf[i - LeftLeafNodeSplitSize]) = leafNode[i - insertionOffset];
                }
            }
            split = rightLeaf;
            return leftLeaf;
        }

        // todo replace with struct enumerator
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _count; ++i) { yield return this[i]; }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private static int GetCount(Array node)
        {
            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);
                return internalNode[internalNode.Length - 1].CumulativeChildCount;
            }
            
            return Unsafe.As<LeafEntry[]>(node).Length;
        }

        // todo remove
        public static ArrayBasedBPlusTreeImmutableList<T> CreateRange(T[] items)
        {
            if (items.Length == 0) { return Empty; }

            var leafQueue = new Queue<LeafEntry[]>();
            var leafBuilder = new List<LeafEntry>();
            for (var i = 0; i < items.Length; ++i)
            {
                leafBuilder.Add(new() { Item = items[i] });
                if (leafBuilder.Count == MaxLeafNodeSize)
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
                if (internalBuilder.Count == MaxInternalNodeSize)
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
                    if (internalBuilder.Count == MaxInternalNodeSize)
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
 
        private static int MaxLeafNodeSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                Unsafe.SizeOf<T>() == 1 ? 128
                : Unsafe.SizeOf<T>() == 2 ? 64
                : Unsafe.SizeOf<T>() == 4 ? 32
                : Unsafe.SizeOf<T>() == 8 ? 16
                : 8;
        }

        private static int LeftLeafNodeSplitSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (MaxLeafNodeSize / 2) + 1;
        }

        private static int MaxInternalNodeSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                Unsafe.SizeOf<T>() == 1 ? 64
                : Unsafe.SizeOf<T>() == 2 ? 32
                : Unsafe.SizeOf<T>() == 4 ? 16
                : 8;
        }

        private static int LeftInternalNodeSplitSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (MaxInternalNodeSize / 2) + 1;
        }

        private struct LeafEntry { public T Item; }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            AssertValid(_root);
            Debug.Assert(Count == GetCount(_root));
        }

        [Conditional("DEBUG")]
        private static void AssertValid(Array node)
        {
            if (node is LeafEntry[] leafNode)
            {
                Debug.Assert(leafNode.Length <= MaxLeafNodeSize);
                return;
            }

            var internalNode = (InternalEntry[])node;
            Debug.Assert(internalNode.Length <= MaxInternalNodeSize);
            for (var i = 0; i < internalNode.Length; ++i)
            {
                AssertValid(internalNode[i].Child);
                Debug.Assert(internalNode[i].CumulativeChildCount - (i > 0 ? internalNode[i - 1].CumulativeChildCount : 0) == GetCount(internalNode[i].Child));
            }
        }
    }
}
