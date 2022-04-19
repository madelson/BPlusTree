using System;
using System.Collections;
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

    public sealed partial class ArrayBasedBPlusTreeImmutableList<T> : IImmutableList<T>, IList<T>, IList, IOrderedCollection<T>, IImmutableListQueries<T>, IStrongEnumerable<T, ArrayBasedBPlusTreeImmutableList<T>.Enumerator>
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
                        if ((force && i != topInternalLevel) ? internalBuilder.Length > 0 : internalBuilder.Length == MaxInternalNodeSize)
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

        public ArrayBasedBPlusTreeImmutableList<T> RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            return RemoveRangeInternal(index, count: 1);
        }

        public ArrayBasedBPlusTreeImmutableList<T> RemoveRange(int index, int count)
        {
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0 || index + count > _count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }

            return count == 0 ? this : RemoveRangeInternal(index, count);
        }

        private ArrayBasedBPlusTreeImmutableList<T> RemoveRangeInternal(int index, int count)
        {
            if (count == _count) { return Empty; }

            Array updated = RemoveRange(_root, index, count, isLeadingEdge: true);

            // collapse now-extraneous levels
            while (updated.Length == 1 && updated.GetType() == typeof(InternalEntry[]))
            {
                updated = Unsafe.As<InternalEntry[]>(updated)[0].Child;
            }

            return new(updated, _count - count);
        }

        private static Array RemoveRange(Array node, int index, int count, bool isLeadingEdge)
        {
            Debug.Assert(index + count <= GetCount(node));
            Debug.Assert(count != 0 && count < GetCount(node));

            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                // identify the range of affected indices
                var low = 0;
                while (internalNode[low].CumulativeChildCount <= index)
                {
                    ++low;
                }
                var high = low;
                while (internalNode[high].CumulativeChildCount < index + count)
                {
                    ++high;
                }

                // recurse on the upper and lower indices of the range if needed
                int lowRelativeIndex = low == 0 ? index : index - internalNode[low - 1].CumulativeChildCount;
                Array? updatedLow = lowRelativeIndex > 0 || (low == high && internalNode[low].CumulativeChildCount > index + count)
                    ? RemoveRange(
                        node: internalNode[low].Child,
                        index: lowRelativeIndex,
                        count: low != high ? Math.Min(internalNode[low].CumulativeChildCount - index, count) : count,
                        isLeadingEdge: isLeadingEdge && low == internalNode.Length - 1)
                    : null;
                Array? updatedHigh = null;
                if (high != low)
                {
                    var highCount = count - (internalNode[high - 1].CumulativeChildCount - index);
                    if (highCount < internalNode[high].CumulativeChildCount - internalNode[high - 1].CumulativeChildCount)
                    {
                        updatedHigh = RemoveRange(
                            node: internalNode[high].Child,
                            index: 0,
                            count: highCount,
                            isLeadingEdge: isLeadingEdge && high == internalNode.Length - 1);
                    }
                }

                // restore node size invariants if needed by merging/redistributing with neighbors
                Array? beforeLow = low > 0 ? internalNode[low - 1].Child : null,
                    afterHigh = high < internalNode.Length - 1 ? internalNode[high + 1].Child : null;
                if (updatedLow != null || updatedHigh != null)
                {
                    var isAfterHighLeadingEdge = high + 2 >= internalNode.Length;
                    RestoreNodeSizeInvariants(ref beforeLow, ref updatedLow, ref updatedHigh, ref afterHigh, isAfterHighLeadingEdge);
                }

                // rebuild the node
                var updatedLength = Math.Max(low - 1, 0) + Math.Max(internalNode.Length - (high + 1) - 1, 0);
                if (beforeLow != null) { ++updatedLength; }
                if (updatedLow != null) { ++updatedLength; }
                if (updatedHigh != null) { ++updatedLength; }
                if (afterHigh != null) { ++updatedLength; }
                var updated = new InternalEntry[updatedLength];
                var i = 0;
                for (; i < low - 1; ++i)
                {
                    updated[i].Child = internalNode[i].Child;
                }
                if (beforeLow != null) { updated[i++].Child = beforeLow; }
                if (updatedLow != null) { updated[i++].Child = updatedLow; }
                if (updatedHigh != null) { updated[i++].Child = updatedHigh; }
                if (afterHigh != null) { updated[i++].Child = afterHigh; }
                for (; i < updated.Length; ++i)
                {
                    updated[i].Child = internalNode[i + (internalNode.Length - updated.Length)].Child;
                }
                SetCumulativeChildCounts(updated);
                return updated;
            }
            else
            {
                LeafEntry[] leafNode = Unsafe.As<LeafEntry[]>(node);
                var updated = new LeafEntry[leafNode.Length - count];
                leafNode.AsSpan(0, index).CopyTo(updated);
                leafNode.AsSpan(index + count).CopyTo(updated.AsSpan(index));
                return updated;
            }
        }

        private static void RestoreNodeSizeInvariants(ref Array? a, ref Array? b, ref Array? c, ref Array? d, bool isLeadingEdge)
        {
            Debug.Assert(b != null || c != null);

            bool isInternal = (b ?? c)!.GetType() == typeof(InternalEntry[]);
            MergeLeft(ref c, ref d, ref isLeadingEdge, isInternal);
            MergeLeft(ref b, ref c, ref isLeadingEdge, isInternal);
            MergeLeft(ref a, ref b, ref isLeadingEdge, isInternal);

            Debug.Assert(a != null);

            static void MergeLeft(ref Array? a, ref Array? b, ref bool isLeadingEdge, bool isInternal)
            {
                if (b is null) 
                { 
                    return; 
                }
                if (a is null) 
                { 
                    a = b;
                    b = null;
                    return;
                }

                int maxNodeSize = isInternal ? MaxInternalNodeSize : MaxLeafNodeSize;
                int minNodeSize = maxNodeSize / 2;
                if (a.Length < minNodeSize || (!isLeadingEdge && b.Length < minNodeSize))
                {
                    int totalLength = a.Length + b.Length;
                    if (totalLength <= maxNodeSize)
                    {
                        Array combined = isInternal ? new InternalEntry[totalLength] : new LeafEntry[totalLength];
                        a.CopyTo(combined, 0);
                        b.CopyTo(combined, a.Length);
                        if (isInternal)
                        {
                            var aCumulativeChildCount = Unsafe.As<InternalEntry[]>(a)[a.Length - 1].CumulativeChildCount;
                            for (var i = a.Length; i < combined.Length; ++i)
                            {
                                Unsafe.As<InternalEntry[]>(combined)[i].CumulativeChildCount += aCumulativeChildCount;
                            }
                        }
                        a = combined;
                        b = null;
                        return;
                    }

                    Array newA, newB;
                    int bLength = isLeadingEdge ? totalLength - maxNodeSize : minNodeSize;
                    if (isInternal)
                    {
                        newA = new InternalEntry[totalLength - bLength];
                        newB = new InternalEntry[bLength];
                    }
                    else
                    {
                        newA = new LeafEntry[totalLength - bLength];
                        newB = new LeafEntry[bLength];
                    }
                    Debug.Assert(newA.Length >= minNodeSize && newB.Length >= (isLeadingEdge ? 0 : minNodeSize));

                    Array.Copy(a, newA, length: Math.Min(a.Length, newA.Length));
                    var aDifference = a.Length - newA.Length;
                    if (aDifference > 0)
                    {
                        Array.Copy(a, newA.Length, newB, 0, length: aDifference);
                        Array.Copy(b, 0, newB, aDifference, b.Length);
                    }
                    else
                    {
                        Array.Copy(b, 0, newA, a.Length, -aDifference);
                        Array.Copy(b, -aDifference, newB, 0, newB.Length);
                    }

                    if (isInternal)
                    {
                        SetCumulativeChildCounts(Unsafe.As<InternalEntry[]>(newA));
                        SetCumulativeChildCounts(Unsafe.As<InternalEntry[]>(newB));
                    }
                    a = newA;
                    b = newB;
                }

                isLeadingEdge = false;
            }
        }

        private static void SetCumulativeChildCounts(InternalEntry[] node)
        {
            var lastCumulativeChildCount = 0;
            for (var i = 0; i < node.Length; ++i)
            {
                lastCumulativeChildCount = node[i].CumulativeChildCount = lastCumulativeChildCount + GetCount(node[i].Child);
            }
        }

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

        public bool IsReadOnly => throw new NotImplementedException();

        bool IList.IsFixedSize => throw new NotImplementedException();

        bool ICollection.IsSynchronized => throw new NotImplementedException();

        object ICollection.SyncRoot => throw new NotImplementedException();

        object? IList.this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        T IList<T>.this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [DebuggerDisplay("{Item}")]
        private struct LeafEntry { public T Item; }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            AssertValid(_root, isLeadingEdge: true);
            Debug.Assert(Count == GetCount(_root));
        }

        [Conditional("DEBUG")]
        private static void AssertValid(Array node, bool isLeadingEdge)
        {
            if (node is LeafEntry[] leafNode)
            {
                Debug.Assert(leafNode.Length <= MaxLeafNodeSize);
                return;
            }

            var internalNode = (InternalEntry[])node;
            Debug.Assert(internalNode.Length >= (isLeadingEdge ? 1 : (MaxInternalNodeSize / 2)));
            Debug.Assert(internalNode.Length <= MaxInternalNodeSize);
            Type childType = internalNode[0].Child.GetType();
            for (var i = 0; i < internalNode.Length; ++i)
            {
                Debug.Assert(!internalNode[i].IsChildMutable);
                Debug.Assert(internalNode[i].Child.GetType() == childType);
                AssertValid(internalNode[i].Child, isLeadingEdge: i == internalNode.Length - 1);
                Debug.Assert(internalNode[i].CumulativeChildCount - (i > 0 ? internalNode[i - 1].CumulativeChildCount : 0) == GetCount(internalNode[i].Child));
            }
        }

        private delegate bool Scanner<TState>(ReadOnlySpan<T> items, ref TState state);

        private static bool ScanForward<TState>(Array node, Scanner<TState> scanner, int startIndex, ref TState state)
        {
            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);
                var childIndex = 0;
                if (startIndex != 0)
                {
                    while (internalNode[childIndex].CumulativeChildCount <= startIndex) { ++childIndex; }
                    if (ScanForward(
                        internalNode[childIndex].Child, 
                        scanner, 
                        startIndex: childIndex == 0 ? startIndex : startIndex - internalNode[childIndex - 1].CumulativeChildCount, 
                        ref state))
                    {
                        return true;
                    }
                    ++childIndex;
                }

                while (childIndex < internalNode.Length)
                {
                    if (ScanForward(internalNode[childIndex++].Child, scanner, startIndex: 0, ref state))
                    {
                        return true;
                    }
                }

                return false;
            }
                
            return scanner(new ReadOnlySpan<T>(Unsafe.As<T[]>(node)).Slice(startIndex), ref state);
        }

        IImmutableList<T> IImmutableList<T>.Add(T value) => Add(value);
        IImmutableList<T> IImmutableList<T>.AddRange(IEnumerable<T> items) => AddRange(items);

        IImmutableList<T> IImmutableList<T>.Clear() => Empty;

        public int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }
            if ((uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange($"{nameof(index)} + {nameof(count)}"); }

            var state = (Remaining: count, item, equalityComparer ?? EqualityComparer<T>.Default);
            return ScanForward(_root, IndexOfDelegate.Instance, index, ref state)
                ? (state.Remaining < 0 ? -1 : index + (count - state.Remaining))
                : -1;
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

        IImmutableList<T> IImmutableList<T>.RemoveAt(int index) => RemoveAt(index);

        IImmutableList<T> IImmutableList<T>.RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.RemoveRange(int index, int count) => RemoveRange(index, count);

        IImmutableList<T> IImmutableList<T>.Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.SetItem(int index, T value) => SetItem(index, value);

        public int IndexOf(T item) => IndexOf(item, 0, _count, null);

        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        void ICollection<T>.Clear() => throw new NotSupportedException();

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNull(nameof(array)); }
            if ((uint)arrayIndex >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(arrayIndex)); }
            // todo different error for this case probably
            if (arrayIndex + _count > array.Length) { ThrowHelper.ThrowArgumentOutOfRange(nameof(array)); }

            var state = (array, arrayIndex);
            ScanForward(_root, CopyToDelegte.Instance, startIndex: 0, ref state);
        }

        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        public ArrayBasedBPlusTreeImmutableList<TOutput> ConvertAll<TOutput>(Func<T, TOutput> converter)
        {
            throw new NotImplementedException();
        }

        public void ForEach(Action<T> action)
        {
            throw new NotImplementedException();
        }

        public ArrayBasedBPlusTreeImmutableList<T> GetRange(int index, int count)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array) => CopyTo(array, 0);

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            throw new NotImplementedException();
        }

        public bool Exists(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public T? Find(Predicate<T> match) 
        {
            Find(startIndex: 0, _count, match, out _, out var foundItem);
            return foundItem;
        }

        public ArrayBasedBPlusTreeImmutableList<T> FindAll(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int FindIndex(Predicate<T> match) =>
            Find(startIndex: 0, count: _count, match, out var foundIndex, out _) ? foundIndex : -1;

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            if ((uint)startIndex >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(startIndex)); }

            return Find(startIndex, _count, match, out var foundIndex, out _) ? foundIndex : -1;
        }

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            if ((uint)startIndex >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(startIndex)); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }
            if ((uint)startIndex + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange($"{nameof(startIndex)} + {nameof(count)}"); }

            return Find(startIndex, count, match, out var foundIndex, out _) ? foundIndex : -1;
        }

        private bool Find(int startIndex, int count, Predicate<T> match, out int foundIndex, out T? foundItem)
        {
            var state = (Predicate: match, Count: count, FoundIndex: startIndex, FoundItem: default(T));
            bool result = ScanForward(_root, FindDelegate.Instance, startIndex, ref state);
            foundIndex = state.FoundIndex;
            foundItem = state.FoundItem;
            return result;
        }

        public T? FindLast(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int FindLastIndex(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public bool TrueForAll(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int BinarySearch(T item)
        {
            throw new NotImplementedException();
        }

        public int BinarySearch(T item, IComparer<T>? comparer)
        {
            throw new NotImplementedException();
        }

        public int BinarySearch(int index, int count, T item, IComparer<T>? comparer)
        {
            throw new NotImplementedException();
        }

        #region IList members
        int IList.Add(object? value) => throw new NotSupportedException();

        void IList.Clear() => throw new NotSupportedException();

        bool IList.Contains(object? value) => IsCompatibleObject(value, out T cast) && Contains(cast);

        int IList.IndexOf(object? value) => IsCompatibleObject(value, out T cast) ? IndexOf(cast) : -1;

        void IList.Insert(int index, object? value) => throw new NotSupportedException();

        void IList.Remove(object? value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }
        #endregion

        private static bool IsCompatibleObject(object? value, out T cast)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
            if (value is T typedValue)
            {
                cast = typedValue;
                return true;
            }
            cast = default!;
            return value == null && default(T) == null;
        }
    }
}
