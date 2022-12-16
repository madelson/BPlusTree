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
        public static readonly ArrayBasedBPlusTreeImmutableList<T> Empty = new(Array.Empty<LeafEntry>(), 0);

        private readonly Array _root;
        private readonly int _count;

        private ArrayBasedBPlusTreeImmutableList(Array root, int count)
        {
            _root = root;
            _count = count;
            AssertValid();
        }

        public int Count => _count;

        public bool IsEmpty => _count == 0;

        public T this[int index] => ItemRef(index);

        public ref readonly T ItemRef(int index)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            return ref ItemRef(_root, index);
        }

        private static ref T ItemRef(Array node, int index)
        {
            while (true)
            {
                if (node.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                    for (var i = 0; i < internalNode.Length; ++i)
                    {
                        if (index < internalNode[i].CumulativeChildCount)
                        {
                            if (i != 0)
                            {
                                index -= internalNode[i - 1].CumulativeChildCount;
                            }
                            node = internalNode[i].Child;
                            break;
                        }
                    }
                    Debug.Assert(node != internalNode);
                }
                else
                {
                    return ref Unsafe.As<LeafEntry[]>(node)[index].Item;
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

        public ArrayBasedBPlusTreeImmutableList<T> InsertRange(int index, IEnumerable<T> items)
        {
            throw new NotImplementedException();
        }

        public ArrayBasedBPlusTreeImmutableList<T> AddRange(IEnumerable<T> items)
        {
            if (items is null) { ThrowHelper.ThrowArgumentNull(nameof(items)); }

            var immutableList = items as ArrayBasedBPlusTreeImmutableList<T>;
            if (immutableList is not null)
            {
                if (this.Count == 0) { return immutableList; }
                if (immutableList.Count == 0) { return this; }

                return new(Concatenate(this._root, immutableList._root), this.Count + immutableList.Count);
            }

            using var enumerator = items.GetEnumerator();

            if (!enumerator.MoveNext()) { return this; }

            // TODO is this right for non-compact this?
            const int MaxInternalLevels = 9;
            ArrayBuilder<ArrayBuilder<InternalEntry>> internalBuilders = new(maxLength: MaxInternalLevels);
            ArrayBuilder<LeafEntry> leafBuilder = new(maxLength: MaxLeafNodeSize);
            PrePopulateBuilders(_root, ref internalBuilders, ref leafBuilder);

            while (true)
            {
                while (leafBuilder.Length < MaxLeafNodeSize)
                {
                    leafBuilder.Add(new() { Item = enumerator.Current });
                    if (!enumerator.MoveNext())
                    {
                        goto Exhausted;
                    }
                }
                AddChild(ref internalBuilders, index: 0, leafBuilder.Length, leafBuilder.MoveToArray());
            }
            Exhausted: 
            if (internalBuilders.Length == 0)
            {
                LeafEntry[] rootLeaf = leafBuilder.MoveToArray();
                return new(rootLeaf, rootLeaf.Length);
            }

            AddChild(ref internalBuilders, index: 0, leafBuilder.Length, leafBuilder.MoveToArray());
            for (var i = 0; i < internalBuilders.Length - 1; ++i)
            {
                ref ArrayBuilder<InternalEntry> internalBuilder = ref internalBuilders[i];
                if (internalBuilder.Length > 0)
                {
                    AddChild(ref internalBuilders, index: i + 1, internalBuilder.Last.CumulativeChildCount, internalBuilder.MoveToArray());
                }
            }
            InternalEntry[] root = internalBuilders.Last.MoveToArray();
            return new(root, root[root.Length - 1].CumulativeChildCount);

            static void AddChild(ref ArrayBuilder<ArrayBuilder<InternalEntry>> internalBuilders, int index, int childCount, Array child)
            {
                int cumulativeChildCount;
                if (index == internalBuilders.Length)
                {
                    internalBuilders.Add(new(maxLength: MaxInternalNodeSize));
                    cumulativeChildCount = childCount;
                }
                else
                {
                    ref ArrayBuilder<InternalEntry> internalBuilder = ref internalBuilders[index];
                    if (internalBuilder.Length == MaxInternalNodeSize)
                    {
                        AddChild(ref internalBuilders, index + 1, internalBuilder.Last.CumulativeChildCount, internalBuilder.MoveToArray());
                        cumulativeChildCount = childCount;
                    }
                    else
                    {
                        cumulativeChildCount = internalBuilder.Length > 0 ? internalBuilder.Last.CumulativeChildCount + childCount : childCount;
                    }
                }

                internalBuilders[index].Add(new() { CumulativeChildCount = cumulativeChildCount, Child = child });
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

        private static Array Concatenate(Array leftRoot, Array rightRoot)
        {
            // TODO could switch to non-allocating stack
            LeafEntry[] leftLeaf = GetPathToEdgeLeaf(leftRoot, isLeft: true, out Stack<InternalEntry[]> leftStack);
            LeafEntry[] rightLeaf = GetPathToEdgeLeaf(rightRoot, isLeft: false, out Stack<InternalEntry[]> rightStack);

            Array left = TryMergeLeaves(leftLeaf, rightLeaf, out Array? right);
            while (leftStack.Count > 0 && rightStack.Count > 0)
            {
                InternalEntry[] leftParent = leftStack.Pop();
                InternalEntry[] rightParent = rightStack.Pop();
                // If the children are both unchanged and the left nodes satisfy the invariants, no-op.
                // We don't need to check invariants on the right node because it will only be smaller than MIN if
                // it is a leading edge and if it is a leading edge we're not altering that.
                if (rightParent[0].Child == right && leftParent.Length >= MaxInternalNodeSize / 2)
                {
                    left = leftParent;
                    right = rightParent;
                }
                else
                {
                    int adjustedRightParentLength = right is null ? rightParent.Length - 1 : rightParent.Length;
                    // case 1: update nodes individually
                    if (leftParent.Length >= MaxInternalNodeSize / 2 && adjustedRightParentLength >= MaxInternalNodeSize / 2)
                    {
                        InternalEntry[] updatedLeftParent = leftParent.Copy();
                        updatedLeftParent[leftParent.Length - 1] = new() { Child = left, CumulativeChildCount = leftParent[leftParent.Length - 2].CumulativeChildCount + GetCount(left) };
                        InternalEntry[] updatedRightParent = rightParent.AsSpan(rightParent.Length - adjustedRightParentLength).ToArray();
                        if (right != null)
                        {
                            updatedRightParent[0].Child = right;
                        }
                        SetCumulativeChildCounts(updatedRightParent);
                        left = updatedLeftParent;
                        right = updatedRightParent;
                    }
                    // case 2: merge nodes
                    else if (leftParent.Length + adjustedRightParentLength <= MaxInternalNodeSize)
                    {
                        var merged = new InternalEntry[leftParent.Length + adjustedRightParentLength];
                        leftParent.AsSpan(0, leftParent.Length - 1).CopyTo(merged);
                        merged[leftParent.Length - 1].Child = left;
                        if (right != null)
                        {
                            merged[leftParent.Length].Child = right;
                        }
                        rightParent.AsSpan(rightParent.Length - adjustedRightParentLength + 1).CopyTo(merged.AsSpan(merged.Length - adjustedRightParentLength + 1));
                        SetCumulativeChildCounts(merged);
                        left = merged;
                        right = null;
                    }
                    // case 3: rebalance nodes
                    else
                    {
                        Rebalance(
                            leftParent, 
                            rightParent.AsSpan(rightParent.Length - adjustedRightParentLength), 
                            out InternalEntry[] updatedLeftParent, 
                            out InternalEntry[] updatedRightParent);
                        if (leftParent.Length - 1 < updatedLeftParent.Length)
                        {
                            updatedLeftParent[leftParent.Length - 1].Child = left;
                        }
                        else
                        {
                            updatedRightParent[leftParent.Length - 1 - updatedLeftParent.Length].Child = left;
                        }
                        if (right != null)
                        {
                            if (leftParent.Length < updatedLeftParent.Length)
                            {
                                updatedLeftParent[leftParent.Length].Child = right;
                            }
                            else
                            {
                                updatedRightParent[leftParent.Length - updatedLeftParent.Length].Child = right;
                            }
                        }
                        SetCumulativeChildCounts(updatedLeftParent);
                        SetCumulativeChildCounts(updatedRightParent);
                        left = updatedLeftParent;
                        right = updatedRightParent;
                    }
                }
            }

            if (leftStack.Count > 0)
            {
                do
                {
                    InternalEntry[] leftParent = leftStack.Pop();
                    InternalEntry[] updatedLeftParent;
                    InternalEntry[]? updatedRightParent;
                    if (right is null)
                    {
                        // TODO repeated
                        updatedLeftParent = leftParent.Copy();
                        updatedLeftParent[leftParent.Length - 1] = new() { Child = left, CumulativeChildCount = leftParent[leftParent.Length - 2].CumulativeChildCount + GetCount(left) };
                        updatedRightParent = null;
                    }
                    else if (leftParent.Length < MaxInternalNodeSize)
                    {
                        updatedLeftParent = new InternalEntry[leftParent.Length + 1];
                        leftParent.AsSpan().CopyTo(updatedLeftParent);
                        int leftCumulativeChildCount = (leftParent.Length > 1 ? leftParent[leftParent.Length - 2].CumulativeChildCount : 0) + GetCount(left);
                        updatedLeftParent[leftParent.Length - 1] = new() { Child = left, CumulativeChildCount = leftCumulativeChildCount };
                        updatedLeftParent[leftParent.Length] = new() { Child = right, CumulativeChildCount = leftCumulativeChildCount + GetCount(right) };
                        updatedRightParent = null;
                    }
                    else
                    {
                        // TODO repeated
                        updatedLeftParent = leftParent.Copy();
                        updatedLeftParent[leftParent.Length - 1] = new() { Child = left, CumulativeChildCount = leftParent[leftParent.Length - 2].CumulativeChildCount + GetCount(left) };
                        updatedRightParent = null;
                        // left-leaning split
                        updatedRightParent = new InternalEntry[] { new() { Child = right, CumulativeChildCount = GetCount(right) } };
                    }
                    left = updatedLeftParent;
                    right = updatedRightParent;
                }
                while (leftStack.Count > 0);
            }
            else
            {
                while (rightStack.Count > 0)
                {
                    InternalEntry[] rightParent = rightStack.Pop();
                    InternalEntry[] updatedLeftParent;
                    InternalEntry[]? updatedRightParent;
                    if (right is null)
                    {
                        // TODO repeated
                        updatedLeftParent = rightParent.Copy();
                        updatedLeftParent[0].Child = left;
                        SetCumulativeChildCounts(updatedLeftParent);
                        updatedRightParent = null;
                    }
                    else if (rightParent.Length < MaxInternalNodeSize)
                    {
                        updatedLeftParent = new InternalEntry[rightParent.Length + 1];
                        rightParent.AsSpan().CopyTo(updatedLeftParent.AsSpan(1));
                        updatedLeftParent[0].Child = left;
                        updatedLeftParent[1].Child = right;
                        SetCumulativeChildCounts(updatedLeftParent);
                        updatedRightParent = null;
                    }
                    else
                    {
                        InternalEntry leftEntry = new() { Child = left };
                        Rebalance(Helpers.CreateReadOnlySpan(ref leftEntry), rightParent, out updatedLeftParent, out updatedRightParent);
                        updatedLeftParent[1].Child = right;
                        SetCumulativeChildCounts(updatedLeftParent);
                        SetCumulativeChildCounts(updatedRightParent);
                    }
                    left = updatedLeftParent;
                    right = updatedRightParent;
                };
            }

            Debug.Assert(GetCount(left) + (right is null ? 0 : GetCount(right)) == GetCount(leftRoot) + GetCount(rightRoot));
            if (right is null)
            {
                return left;
            }

            var mergedRoot = new InternalEntry[] { new() { Child = left }, new() { Child = right } };
            SetCumulativeChildCounts(mergedRoot);
            return mergedRoot;

            static LeafEntry[] GetPathToEdgeLeaf(Array root, bool isLeft, out Stack<InternalEntry[]> stack)
            {
                stack = new();
                Array current = root;
                while (current.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] internalCurrent = Unsafe.As<InternalEntry[]>(current);
                    stack.Push(internalCurrent);
                    current = internalCurrent[isLeft ? internalCurrent.Length - 1 : 0].Child;
                }
                return Unsafe.As<LeafEntry[]>(current);
            }

            static LeafEntry[] TryMergeLeaves(LeafEntry[] left, LeafEntry[] right, out Array? unmerged)
            {
                // case 1: both can be left alone
                if (left.Length >= MaxLeafNodeSize / 2 && right.Length >= MaxLeafNodeSize / 2)
                {
                    unmerged = right;
                    return left;
                }

                // case 2: merge into single node
                int totalLength = left.Length + right.Length;
                if (totalLength < MaxLeafNodeSize)
                {
                    var merged = new LeafEntry[totalLength];
                    left.AsSpan().CopyTo(merged);
                    right.AsSpan().CopyTo(merged.AsSpan(left.Length));
                    unmerged = null;
                    return merged;
                }

                // case 3: rebalance
                Rebalance(left, right, out LeafEntry[] updatedLeft, out LeafEntry[] updatedRight);
                unmerged = updatedRight;
                return updatedLeft;
            }

            static void Rebalance<E>(ReadOnlySpan<E> left, ReadOnlySpan<E> right, out E[] updatedLeft, out E[] updatedRight)
            {
                int totalLength = left.Length + right.Length;
                updatedRight = new E[totalLength / 2];
                updatedLeft = new E[totalLength - updatedRight.Length];
                for (var i = 0; i < totalLength; ++i)
                {
                    ref readonly E source = ref (i < left.Length ? ref left[i] : ref right[i - left.Length]);
                    ref E destination = ref (i < updatedLeft.Length ? ref updatedLeft[i] : ref updatedRight[i - updatedLeft.Length]);
                    destination = source;
                }
            }
        }

        public ArrayBasedBPlusTreeImmutableList<T> Clear() => Empty;

        /// <summary>
        /// See the <see cref="IImmutableList{T}"/> interface.
        /// </summary>
        public ArrayBasedBPlusTreeImmutableList<T> Remove(T value) => Remove(value, equalityComparer: null);

        /// <summary>
        /// See the <see cref="IImmutableList{T}"/> interface.
        /// </summary>
        public ArrayBasedBPlusTreeImmutableList<T> Remove(T value, IEqualityComparer<T>? equalityComparer)
        {
            int index = IndexOf(value, index: 0, _count, equalityComparer);
            return index < 0 ? this : RemoveAt(index);
        }
 
        public ArrayBasedBPlusTreeImmutableList<T> RemoveAll(Predicate<T> match)
        {
            if (match is null) { ThrowHelper.ThrowArgumentNull(nameof(match)); }

            var result = this;
            var index = 0;
            Predicate<T>? negatedMatch = null;
            while (index < result._count)
            {
                int removeStartIndex = result.FindIndex(index, match);
                if (removeStartIndex < 0)
                {
                    break;
                }

                int removeEndIndex = result.FindIndex(removeStartIndex + 1, negatedMatch ??= i => !match(i));
                int removeCount = (removeEndIndex < 0 ? result._count : removeEndIndex) - removeStartIndex;
                result = result.RemoveRange(removeStartIndex, removeCount);

                // We just removed [removeStartIndex..removeEndIndex]. One might think the search should start
                // again from removeStartIndex. However, we know that that is not a match because otherwise
                // it would have been part of the removed range. Therefore, removeStartIndex + 1 is the first
                // index we haven't tested.
                index = removeStartIndex + 1;
            }

            return result;
        }

        public ArrayBasedBPlusTreeImmutableList<T> RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            return RemoveRangeInternal(index, count: 1);
        }

        public ArrayBasedBPlusTreeImmutableList<T> RemoveRange(int index, int count)
        {
            // todo be more consistent about checking (index, count)
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0 || (uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }

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

        public ArrayBasedBPlusTreeImmutableList<T> RemoveRange(IEnumerable<T> items) => RemoveRange(items, equalityComparer: null);

        public ArrayBasedBPlusTreeImmutableList<T> RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
        {
            if (items is null) { ThrowHelper.ThrowArgumentNull(nameof(items)); }

            if (IsEmpty) { return this; }

            Builder? builder = null;
            foreach (var item in items)
            {
                if (builder is null)
                {
                    int index = IndexOf(item, 0, _count, equalityComparer);
                    if (index >= 0)
                    {
                        builder = ToBuilder();
                        builder.RemoveAt(index);
                    }
                }
                else
                {
                    int index = builder.IndexOf(item, 0, builder.Count, equalityComparer);
                    if (index >= 0)
                    {
                        builder.RemoveAt(index);
                    }
                }
            }

            return builder?.ToImmutable() ?? this;
        }

        // TODO: need to "split" the tree around the span to be removed, then stitch it back together
        // so that we can fix up sizes. But how do we handle splitting off the RHS while maintaining invariants?
        // Key is that when merging 2 nodes the merge has to be recursive along the "fault line"
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
                    var isAfterHighLeadingEdge = isLeadingEdge && high + 2 >= internalNode.Length;
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
            bool isMergingLeadingEdge = isLeadingEdge;
            MergeLeft(ref c, ref d, ref isMergingLeadingEdge, isInternal);
            MergeLeft(ref b, ref c, ref isMergingLeadingEdge, isInternal);
            MergeLeft(ref a, ref b, ref isMergingLeadingEdge, isInternal);

            Debug.Assert(a != null);
            Debug.Assert(
                new[] { a, b, c, d }.Where(n => n != null)
                    .Reverse()
                    .Skip(isLeadingEdge ? 1 : 0)
                    .All(n => n!.Length >= (isInternal ? MaxInternalNodeSize : MaxLeafNodeSize) / 2)
            );

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

        public ArrayBasedBPlusTreeImmutableList<T> GetRange(int index, int count)
        {
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            if (count == 0) { return Empty; }

            if (count < 0 || (uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }

            ArrayBasedBPlusTreeImmutableList<T> result = this;
            if (index > 0)
            {
                result = result.RemoveRangeInternal(0, index);
            }
            int trimFromEndCount = _count - index - count;
            if (trimFromEndCount > 0)
            {
                result = result.RemoveRangeInternal(result.Count - trimFromEndCount, trimFromEndCount);
            }
            return result;
        }

        /// <summary>
        /// See the <see cref="IImmutableList{T}"/> interface.
        /// </summary>
        public ArrayBasedBPlusTreeImmutableList<T> Replace(T oldValue, T newValue) => Replace(oldValue, newValue, EqualityComparer<T>.Default);

        /// <summary>
        /// See the <see cref="IImmutableList{T}"/> interface.
        /// </summary>
        public ArrayBasedBPlusTreeImmutableList<T> Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
        {
            int index = IndexOf(oldValue, 0, _count, equalityComparer);
            if (index < 0) { ThrowHelper.ThrowCannotFindOldValue(); }

            return SetItem(index, newValue);
        }

        public ArrayBasedBPlusTreeImmutableList<T> Reverse() => Reverse(0, _count);

        public ArrayBasedBPlusTreeImmutableList<T> Reverse(int index, int count)
        {
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0 || (uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }

            if (count <= 1) { return this; }

            Array clonedRoot = CloneRange(_root, index, count);
            var low = index;
            var high = index + count - 1;
            do
            {
                ref T lowItemRef = ref ItemRef(clonedRoot, low);
                ref T highItemRef = ref ItemRef(clonedRoot, high);
                (highItemRef, lowItemRef) = (lowItemRef, highItemRef);
            }
            while (--high > ++low);

            return new(clonedRoot, _count);

            static Array CloneRange(Array node, int index, int count)
            {
                Debug.Assert(index >= 0);
                Debug.Assert(count > 0 && index + count <= GetCount(node));

                if (node.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] clonedNode = Unsafe.As<InternalEntry[]>(node).Copy();

                    // identify the range of affected indices
                    var low = 0;
                    while (clonedNode[low].CumulativeChildCount <= index)
                    {
                        ++low;
                    }
                    var high = low;
                    while (clonedNode[high].CumulativeChildCount < index + count)
                    {
                        ++high;
                    }

                    for (var i = low; i <= high; ++i)
                    {
                        int previousCumulativeChildCount = i == 0 ? 0 : clonedNode[i - 1].CumulativeChildCount;
                        int childIndex = i == low ? index - previousCumulativeChildCount : 0;
                        int childCount = (i == high ? count + index : clonedNode[i].CumulativeChildCount) - previousCumulativeChildCount - childIndex;
                        clonedNode[i].Child = CloneRange(clonedNode[i].Child, childIndex, childCount);
                    }

                    return clonedNode;
                }

                return Unsafe.As<LeafEntry[]>(node).Copy();
            }
        }

        public ArrayBasedBPlusTreeImmutableList<T> Sort() => Sort(default(IComparer<T>));

        public ArrayBasedBPlusTreeImmutableList<T> Sort(Comparison<T> comparison)
        {
            if (comparison is null) { ThrowHelper.ThrowArgumentNull(nameof(comparison)); }
            return Sort(Comparer<T>.Create(comparison));
        }

        public ArrayBasedBPlusTreeImmutableList<T> Sort(IComparer<T>? comparer) => Sort(0, _count, comparer);

        public ArrayBasedBPlusTreeImmutableList<T> Sort(int index, int count, IComparer<T>? comparer)
        {
            if ((uint)index > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0 || (uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }

            if (count <= 1) { return this; }

            var array = new T[count];
            this.CopyTo(index, array, arrayIndex: 0, count);
            Array.Sort(array, comparer);

            var arrayIndex = 0;
            return new(SetRangeFromArray(_root, index, count, array, ref arrayIndex), _count);

            static Array SetRangeFromArray(Array node, int index, int count, T[] array, ref int arrayIndex)
            {
                Debug.Assert(index + count <= GetCount(node));
                Debug.Assert(count != 0 && count <= GetCount(node));

                if (node.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] updatedInternalNode = Unsafe.As<InternalEntry[]>(node).Copy();
                    int childIndex = 0;
                    while (updatedInternalNode[childIndex].CumulativeChildCount <= index)
                    {
                        ++childIndex;
                    }

                    do
                    {
                        MapStartIndexAndCountToChild(updatedInternalNode, childIndex, index, count, out int childStartIndex, out int childCount);
                        updatedInternalNode[childIndex].Child = SetRangeFromArray(updatedInternalNode[childIndex].Child, childStartIndex, childCount, array, ref arrayIndex);
                    } while (updatedInternalNode[childIndex].CumulativeChildCount < index + count && ++childIndex < updatedInternalNode.Length);

                    return updatedInternalNode;
                }

                LeafEntry[] updatedLeafNode = count < node.Length
                    ? Unsafe.As<LeafEntry[]>(node).Copy()
                    : new LeafEntry[node.Length];
                for (int i = index; i < index + count; ++i)
                {
                    updatedLeafNode[i].Item = array[arrayIndex++];
                }
                return updatedLeafNode;
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

        // TODO does it make any sense to vary internal node sizes?
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

        bool ICollection<T>.IsReadOnly => true;

        bool IList.IsReadOnly => true;

        bool IList.IsFixedSize => throw new NotImplementedException();

        bool ICollection.IsSynchronized => throw new NotImplementedException();

        object ICollection.SyncRoot => throw new NotImplementedException();

        object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        T IList<T>.this[int index] { get => this[index]; set => throw new NotSupportedException(); }

        [DebuggerDisplay("{Item}")]
        private struct LeafEntry { public T Item; }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            AssertValid(_root, NodeType.Root);
            Debug.Assert(Count == GetCount(_root));
        }

        private enum NodeType { Root, LeadingEdge, Normal }

        [Conditional("DEBUG")]
        private static void AssertValid(Array node, NodeType nodeType)
        {
            if (node is LeafEntry[] leafNode)
            {
                Debug.Assert(nodeType != NodeType.Normal || leafNode.Length >= MaxLeafNodeSize / 2);
                Debug.Assert(leafNode.Length <= MaxLeafNodeSize);
                return;
            }

            var internalNode = (InternalEntry[])node;
            int minNodeLength = nodeType switch
            {
                NodeType.Root => 2,
                NodeType.LeadingEdge => 1,
                NodeType.Normal => MaxInternalNodeSize / 2,
                _ => throw new ArgumentException(nameof(nodeType))
            };
            Debug.Assert(internalNode.Length >= minNodeLength);
            Debug.Assert(internalNode.Length <= MaxInternalNodeSize);
            Type childType = internalNode[0].Child.GetType();
            for (var i = 0; i < internalNode.Length; ++i)
            {
                Debug.Assert(!internalNode[i].IsChildMutable);
                Debug.Assert(internalNode[i].Child.GetType() == childType);
                AssertValid(internalNode[i].Child, i == internalNode.Length - 1 ? NodeType.LeadingEdge : NodeType.Normal);
                Debug.Assert(internalNode[i].CumulativeChildCount - (i > 0 ? internalNode[i - 1].CumulativeChildCount : 0) == GetCount(internalNode[i].Child));
            }
        }

        private delegate bool Scanner<TState>(ReadOnlySpan<T> items, ref TState state);

        // todo we could incorporate count as well as startIndex here
        private static bool ScanForward<TState>(Array node, Scanner<TState> scanner, int startIndex, int count, ref TState state)
        {
            Debug.Assert(startIndex >= 0 && startIndex < GetCount(node));
            Debug.Assert(count > 0 && startIndex + count <= GetCount(node));

            if (node.GetType() == typeof(InternalEntry[]))
            {
                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                var childIndex = 0;
                while (internalNode[childIndex].CumulativeChildCount <= startIndex) { ++childIndex; }

                do
                {
                    MapStartIndexAndCountToChild(internalNode, childIndex, startIndex, count, out int childStartIndex, out int childCount);
                    if (childCount <= 0) 
                    { 
                        return false; 
                    }

                    if (ScanForward(internalNode[childIndex].Child, scanner, childStartIndex, childCount, ref state))
                    {
                        return true;
                    }
                }
                while (++childIndex < internalNode.Length);
                
                return false;
            }
                
            return scanner(new ReadOnlySpan<T>(Unsafe.As<T[]>(node), startIndex, count), ref state);
        }

        private static void MapStartIndexAndCountToChild(InternalEntry[] node, int childIndex, int startIndex, int count, out int childStartIndex, out int childCount)
        {
            if (childIndex == 0)
            {
                childStartIndex = startIndex;
                childCount = Math.Min(count, node[childIndex].CumulativeChildCount - startIndex);
            }
            else
            {
                int childOffset = node[childIndex - 1].CumulativeChildCount;
                if (childOffset >= startIndex)
                {
                    childStartIndex = 0;
                    childCount = Math.Min(startIndex + count, node[childIndex].CumulativeChildCount) - childOffset;
                }
                else
                {
                    childStartIndex = startIndex - childOffset;
                    childCount = Math.Min(count, node[childIndex].CumulativeChildCount - childStartIndex - childOffset);
                }
            }

            Debug.Assert(childStartIndex >= 0 && childStartIndex < GetCount(node[childIndex].Child));
            Debug.Assert(childCount <= GetCount(node[childIndex].Child));
        }

        IImmutableList<T> IImmutableList<T>.Add(T value) => Add(value);
        IImmutableList<T> IImmutableList<T>.AddRange(IEnumerable<T> items) => AddRange(items);

        IImmutableList<T> IImmutableList<T>.Clear() => Empty;

        public int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }
            if ((uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange($"{nameof(index)} + {nameof(count)}"); }

            var state = (Index: index, item, equalityComparer ?? EqualityComparer<T>.Default);
            return ScanForward(_root, IndexOfDelegate.Instance, index, count, ref state)
                ? state.Index
                : -1;
        }

        IImmutableList<T> IImmutableList<T>.Insert(int index, T item) => Insert(index, item);

        IImmutableList<T> IImmutableList<T>.InsertRange(int index, IEnumerable<T> items) => InsertRange(index, items);

        public int LastIndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
        {
            throw new NotImplementedException();
        }

        IImmutableList<T> IImmutableList<T>.Remove(T value, IEqualityComparer<T>? equalityComparer) => Remove(value, equalityComparer);

        IImmutableList<T> IImmutableList<T>.RemoveAll(Predicate<T> match) => RemoveAll(match);

        IImmutableList<T> IImmutableList<T>.RemoveAt(int index) => RemoveAt(index);

        IImmutableList<T> IImmutableList<T>.RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer) => RemoveRange(items, equalityComparer);

        IImmutableList<T> IImmutableList<T>.RemoveRange(int index, int count) => RemoveRange(index, count);

        IImmutableList<T> IImmutableList<T>.Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer) => Replace(oldValue, newValue, equalityComparer);

        IImmutableList<T> IImmutableList<T>.SetItem(int index, T value) => SetItem(index, value);

        public int IndexOf(T item) => IndexOf(item, 0, _count, null);

        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        void ICollection<T>.Clear() => throw new NotSupportedException();

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array) => CopyTo(array, 0);

        public void CopyTo(T[] array, int arrayIndex) => CopyTo(0, array, arrayIndex, _count);

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }
            if ((uint)index + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange($"{nameof(index)} + {nameof(count)}"); }

            if (array is null) { ThrowHelper.ThrowArgumentNull(nameof(array)); }
            if ((uint)arrayIndex >= (uint)array.Length) { ThrowHelper.ThrowArgumentOutOfRange(nameof(arrayIndex)); }
            // todo different error for this case probably
            if (arrayIndex + count > array.Length) { ThrowHelper.ThrowArgumentOutOfRange(nameof(array)); }

            var state = (array, arrayIndex);
            ScanForward(_root, CopyToDelegte.Instance, startIndex: index, count, ref state);
        }

        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        public ArrayBasedBPlusTreeImmutableList<TOutput> ConvertAll<TOutput>(Func<T, TOutput> converter)
        {
            if (converter is null) { ThrowHelper.ThrowArgumentNull(nameof(converter)); }

            return ArrayBasedBPlusTreeImmutableList.CreateRange(this.Select(converter));
        }

        public void ForEach(Action<T> action)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNull(nameof(action)); }

            ScanForward(_root, ForEachDelegate.Instance, startIndex: 0, _count, ref action);
        }

        public bool Exists(Predicate<T> match) => FindIndex(match) >= 0;

        public T? Find(Predicate<T> match) 
        {
            Find(startIndex: 0, _count, match, out _, out var foundItem);
            return foundItem;
        }

        public ArrayBasedBPlusTreeImmutableList<T> FindAll(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public int FindIndex(Predicate<T> match) => FindIndex(startIndex: 0, match);

        public int FindIndex(int startIndex, Predicate<T> match) => FindIndex(startIndex, _count - startIndex, match);

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            if ((uint)startIndex >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(nameof(startIndex)); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRange(nameof(count)); }
            if ((uint)startIndex + (uint)count > (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange($"{nameof(startIndex)} + {nameof(count)}"); }

            return Find(startIndex, count, match, out var foundIndex, out _) ? foundIndex : -1;
        }

        private bool Find(int startIndex, int count, Predicate<T> match, out int foundIndex, out T? foundItem)
        {
            if (match is null) { ThrowHelper.ThrowArgumentNull(nameof(match)); }

            var state = (Predicate: match, FoundIndex: startIndex, FoundItem: default(T));
            bool result = ScanForward(_root, FindDelegate.Instance, startIndex, count, ref state);
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
            if (match is null) { ThrowHelper.ThrowArgumentNull(nameof(match)); }

            return !ScanForward(_root, TrueForAllDelegate.Instance, startIndex: 0, _count, ref match);
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
