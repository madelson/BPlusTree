using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using InternalEntry = ArrayBasedBPlusTreeImmutableListInternalEntry;

    public sealed class ArrayBasedBPlusTreeImmutableList<T> : IImmutableList<T>
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

        public ArrayBasedBPlusTreeImmutableList<T> SetItem(int index, T item)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            return new(SetItem(_root, index, item), _count);
        }

        private static Array SetItem(Array node, int index, T item)
        {
            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                var childIndex = 0;
                while (childIndex < internalNode.Length - 1 && internalNode[childIndex].CumulativeChildCount <= index) { ++childIndex; }

                var updatedNode = internalNode.Copy();
                updatedNode[childIndex].Child = SetItem(
                    updatedNode[childIndex].Child,
                    childIndex == 0 ? index : index - updatedNode[childIndex - 1].CumulativeChildCount,
                    item);
                return updatedNode;
            }
            else
            {
                LeafEntry[] updatedLeafNode = Unsafe.As<LeafEntry[]>(node).Copy();
                updatedLeafNode[index].Item = item;
                return updatedLeafNode;
            }
        }

        public ArrayBasedBPlusTreeImmutableList<T> Add(T item)
        {
            Array updated = Add(_root, item, out bool isSplit);
            return isSplit
                ? new(new InternalEntry[] { new() { Child = _root, CumulativeChildCount = _count }, new() { Child = updated, CumulativeChildCount = _count + 1 } }, _count + 1)
                : new(updated, _count + 1);
        }

        private static Array Add(Array node, T item, out bool isSplit)
        {
            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                Array updatedChild = Add(internalNode[internalNode.Length - 1].Child, item, out bool isChildSplit);

                // case 1: update
                if (!isChildSplit)
                {
                    var updated = new InternalEntry[internalNode.Length];
                    internalNode.AsSpan().CopyTo(updated);
                    updated[updated.Length - 1].Child = updatedChild;
                    ++updated[updated.Length - 1].CumulativeChildCount;
                    isSplit = false;
                    return updated;
                }

                Debug.Assert(GetCount(updatedChild) == 1);

                // case 2: expand
                if (internalNode.Length < MaxInternalNodeSize)
                {
                    var expanded = new InternalEntry[internalNode.Length + 1];
                    internalNode.AsSpan().CopyTo(expanded);
                    expanded[expanded.Length - 1] = new()
                    {
                        Child = updatedChild,
                        CumulativeChildCount = expanded.Length > 1 ? expanded[expanded.Length - 2].CumulativeChildCount + 1 : 1
                    };
                    isSplit = false;
                    return expanded;
                }

                // case 3: "left-leaning" split
                isSplit = true;
                return new InternalEntry[] { new() { Child = updatedChild, CumulativeChildCount = 1 } };
            }

            LeafEntry[] leafNode = Unsafe.As<LeafEntry[]>(node);

            // case 1: expand
            if (leafNode.Length < MaxLeafNodeSize)
            {
                var expandedLeaf = new LeafEntry[leafNode.Length + 1];
                leafNode.AsSpan().CopyTo(expandedLeaf);
                expandedLeaf[expandedLeaf.Length - 1].Item = item;
                isSplit = false;
                return expandedLeaf;
            }

            // case 2: "left-leaning" split
            isSplit = true;
            return new LeafEntry[] { new() { Item = item } };
        }

        public ArrayBasedBPlusTreeImmutableList<T> Insert(int index, T item)
        {
            var count = _count;
            if ((uint)index >= (uint)count) 
            { 
                if (index == count)
                {
                    return Add(item);
                }
                ThrowHelper.ThrowArgumentOutOfRange(); 
            }

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

        public ArrayBasedBPlusTreeImmutableList<T> AddRange(IEnumerable<T> items)
        {
            if (items is null) { ThrowHelper.ThrowArgumentNull(nameof(items)); }

            var immutableList = items as ArrayBasedBPlusTreeImmutableList<T>;
            if (immutableList is not null)
            {
                if (this.Count == 0) { return immutableList; }
                if (immutableList.Count == 0) { return this; }

                // todo we can further optimize this case by "stitching together"
                // the two lists, preserving most existing nodes
            }

            using var enumerator = items.GetEnumerator();

            if (!enumerator.MoveNext()) { return this; }

            const int MaxInternalLevels = 9;
            ArrayBuilder<ArrayBuilder<InternalEntry>> internalBuilders = new(maxLength: MaxInternalLevels);
            ArrayBuilder<LeafEntry> leafBuilder = new(maxLength: MaxLeafNodeSize);
            PrePopulateBuilders(_root, ref internalBuilders, ref leafBuilder);

            do
            {
                leafBuilder.Add(new() { Item = enumerator.Current });
                CompleteNodes(ref internalBuilders, ref leafBuilder, force: false);
            } while (enumerator.MoveNext());
            CompleteNodes(ref internalBuilders, ref leafBuilder, force: true);

            if (internalBuilders.Length > 0)
            {
                InternalEntry[] root = internalBuilders.Last.MoveToArray();
                return new(root, root[root.Length - 1].CumulativeChildCount);
            }

            LeafEntry[] rootLeaf = leafBuilder.MoveToArray();
            return new(rootLeaf, rootLeaf.Length);

            static void CompleteNodes(
                ref ArrayBuilder<ArrayBuilder<InternalEntry>> internalBuilders, 
                ref ArrayBuilder<LeafEntry> leafBuilder,
                bool force)
            {
                var addedLeaf = false;
                if (force ? (leafBuilder.Length > 0 && internalBuilders.Length > 0) : leafBuilder.Length == MaxLeafNodeSize)
                {
                    if (internalBuilders.Length == 0) { internalBuilders.Add(new(maxLength: MaxInternalNodeSize)); }
                    int cumulativeChildCount = internalBuilders[0].Length > 0 ? internalBuilders[0].Last.CumulativeChildCount + leafBuilder.Length : leafBuilder.Length;
                    internalBuilders[0].Add(new() { CumulativeChildCount = cumulativeChildCount, Child = leafBuilder.MoveToArray() });
                    addedLeaf = true;
                }

                if (force || addedLeaf)
                {
                    int topInternalLevel = internalBuilders.Length - 1;
                    for (var i = 0; i <= topInternalLevel; ++i)
                    {
                        ref ArrayBuilder<InternalEntry> internalBuilder = ref internalBuilders[i];
                        if (force ? internalBuilder.Length > (i == topInternalLevel ? 1 : 0) : internalBuilder.Length == MaxInternalNodeSize)
                        {
                            if (i == topInternalLevel) { internalBuilders.Add(new(maxLength: MaxInternalNodeSize)); }
                            int cumulativeChildCount = internalBuilders[i + 1].Length > 0 
                                ? internalBuilders[i + 1].Last.CumulativeChildCount + internalBuilder.Last.CumulativeChildCount 
                                : internalBuilder.Last.CumulativeChildCount;
                            internalBuilders[i + 1].Add(new() { CumulativeChildCount = cumulativeChildCount, Child = internalBuilder.MoveToArray() });
                        }
                        else if (!force) { break; }
                    }
                }
            }

            static void PrePopulateBuilders(Array root, ref ArrayBuilder<ArrayBuilder<InternalEntry>> internalBuilders, ref ArrayBuilder<LeafEntry> leafBuilder)
            {
                ArrayBuilder<Array> leadingNodes = new(maxLength: MaxInternalLevels + 1);
                Array current = root;
                while (true)
                {
                    leadingNodes.Add(current);
                    if (current.GetType() == typeof(InternalEntry[]))
                    {
                        internalBuilders.Add(new(maxLength: MaxInternalNodeSize));
                        current = Unsafe.As<InternalEntry[]>(current)[current.Length - 1].Child;
                    }
                    else
                    {
                        break;
                    }
                }

                var firstIndexToOmitFromBuilders = leadingNodes.Length;
                for (int i = leadingNodes.Length - 1; i >= 0; --i)
                {
                    Array node = leadingNodes[i];
                    if (node.Length == (node.GetType() == typeof(InternalEntry[]) ? MaxInternalNodeSize : MaxLeafNodeSize))
                    {
                        firstIndexToOmitFromBuilders = i;
                    }
                    else
                    {
                        break; // once a node is added, all of its parents are added too
                    }
                }

                for (var i = 0; i < firstIndexToOmitFromBuilders; ++i)
                {
                    Array node = leadingNodes[i];
                    if (node.GetType() == typeof(InternalEntry[]))
                    {
                        InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);
                        internalBuilders[internalBuilders.Length - 1 - i]
                            .AddRange(internalNode.AsSpan(0, firstIndexToOmitFromBuilders == i + 1 ? internalNode.Length : internalNode.Length - 1));
                    }
                    else
                    {
                        leafBuilder.AddRange(Unsafe.As<LeafEntry[]>(node));
                    }
                }
            }
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

        IImmutableList<T> IImmutableList<T>.Add(T value) => Add(value);
        IImmutableList<T> IImmutableList<T>.AddRange(IEnumerable<T> items) => AddRange(items);

        IImmutableList<T> IImmutableList<T>.Clear()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.Insert(int index, T item) => Insert(index, item);

        IImmutableList<T> IImmutableList<T>.InsertRange(int index, IEnumerable<T> items)
        {
            throw new NotImplementedException();
        }

        public int LastIndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.Remove(T value, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.RemoveAll(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.RemoveRange(int index, int count)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.SetItem(int index, T value) => SetItem(index, value);
    }
}
