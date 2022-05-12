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

    public partial class ArrayBasedBPlusTreeImmutableList<T>
    {
        public Builder ToBuilder() => new(_root, _count);

        public sealed class Builder : IList<T>, IList, IOrderedCollection<T>, IImmutableListQueries<T>, IReadOnlyList<T>
        {
            private Array _root;
            private int _count;
            private int _version;
            private bool _isRootMutable;

            internal Builder(Array root, int count)
            {
                _root = root;
                _count = count;
            }

            internal int Version => _version;

            public int Count => _count;

            public T this[int index] 
            { 
                get
                {
                    if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

                    Array current = _root;
                    while (current.GetType() == typeof(InternalEntry[]))
                    {
                        InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(current);

                        var childIndex = 0;
                        while (childIndex < internalNode.Length - 1 && internalNode[childIndex].CumulativeChildCountForBuilder <= index) { ++childIndex; }
                        
                        if (childIndex > 0) { index -= internalNode[childIndex - 1].CumulativeChildCountForBuilder; }
                        current = internalNode[childIndex].Child;
                    }

                    return Unsafe.As<LeafEntry[]>(current)[index].Item;
                }
                set
                {
                    if ((uint)index >= (uint)_count) { ThrowHelper.ThrowArgumentOutOfRange(); }

                    if (SetItem(_root, _isRootMutable, index, value) is { } newRoot)
                    {
                        Debug.Assert(!_isRootMutable);
                        _root = newRoot;
                        _isRootMutable = true;
                    }
                }
            }

            bool ICollection<T>.IsReadOnly => false;

            private Array? SetItem(Array node, bool isMutable, int index, T item)
            {
                if (node.GetType() == typeof(InternalEntry[]))
                {
                    var internalNode = Unsafe.As<InternalEntry[]>(node);

                    var childIndex = 0;
                    while (childIndex < internalNode.Length - 1 && internalNode[childIndex].CumulativeChildCountForBuilder <= index) { ++childIndex; }

                    int adjustedIndex = childIndex == 0 ? index : index - internalNode[childIndex - 1].CumulativeChildCountForBuilder;
                    if (SetItem(internalNode[childIndex].Child, internalNode[childIndex].IsChildMutable, adjustedIndex, item) is { } newChild)
                    {
                        Debug.Assert(!internalNode[childIndex].IsChildMutable);
                        if (!isMutable)
                        {
                            InternalEntry[] mutable = internalNode.Copy();
                            mutable[childIndex].Child = newChild;
                            mutable[childIndex].IsChildMutable = true;
                            return mutable;
                        }

                        internalNode[childIndex].Child = newChild;
                        internalNode[childIndex].IsChildMutable = true;
                    }
                }
                else
                {
                    var leafNode = Unsafe.As<LeafEntry[]>(node);
                    if (!isMutable)
                    {
                        LeafEntry[] mutable = leafNode.Copy();
                        mutable[index].Item = item;
                        return mutable;
                    }

                    leafNode[index].Item = item;
                }

                return null;
            }

            public ArrayBasedBPlusTreeImmutableList<T> ToImmutable()
            {
                if (_isRootMutable)
                {
                    Freeze(ref _root, _count);
                    _isRootMutable = false;
                }

                return new(_root, _count);
            }

            private static void Freeze(ref Array node, int count)
            {
                if (node.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);
                    var previousCumulativeChildCount = 0;
                    for (var i = 0; i < internalNode.Length; ++i)
                    {
                        if (internalNode[i].IsChildMutable)
                        {
                            Freeze(ref internalNode[i].Child, internalNode[i].CumulativeChildCountForBuilder - previousCumulativeChildCount);
                            internalNode[i].IsChildMutable = false;
                        }
                        previousCumulativeChildCount = internalNode[i].CumulativeChildCountForBuilder;
                    }
                }
                // builder leaves can have extra "slack" to minimize allocations; resize as needed
                else if (node.Length != count)
                {
                    Debug.Assert(node.Length > count && node.Length == MaxLeafNodeSize); // should be a pre-expanded node
                    var resized = new LeafEntry[count];
                    Unsafe.As<LeafEntry[]>(node).AsSpan(0, count).CopyTo(resized);
                    node = resized;
                } 
            }

            public void Add(T item)
            {
                Array updated = Add(_root, _count, item, _isRootMutable, out bool isSplit);
                ++_version;
                _root = isSplit
                    ? new InternalEntry[]
                    {
                        new() { Child = _root, CumulativeChildCountForBuilder = _count, IsChildMutable = _isRootMutable },
                        InternalEntry.CreateMutable(updated, _count + 1),
                    }
                    : updated;
                _isRootMutable = true;
                ++_count;
            }

            private static Array Add(Array node, int count, T item, bool isMutable, out bool isSplit)
            {
                if (node.GetType() == typeof(InternalEntry[]))
                {
                    InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);

                    int lastChildCount = internalNode.Length == 1 ? count : count - internalNode[internalNode.Length - 2].CumulativeChildCountForBuilder;
                    Array updatedChild = Add(
                        internalNode[internalNode.Length - 1].Child, 
                        lastChildCount, 
                        item, 
                        internalNode[internalNode.Length - 1].IsChildMutable, 
                        out bool isChildSplit);

                    // case 1: update
                    if (!isChildSplit)
                    {
                        isSplit = false;

                        if (isMutable) // update in place
                        {
                            internalNode[internalNode.Length - 1] = InternalEntry.CreateMutable(
                                updatedChild,
                                internalNode[internalNode.Length - 1].CumulativeChildCountForBuilder + 1
                            );
                            return internalNode;
                        }

                        var updated = new InternalEntry[internalNode.Length];
                        internalNode.AsSpan().CopyTo(updated);
                        updated[updated.Length - 1] = InternalEntry.CreateMutable(updatedChild, updated[updated.Length - 1].CumulativeChildCountForBuilder + 1);
                        return updated;
                    }

                    // case 2: expand
                    if (internalNode.Length < MaxInternalNodeSize)
                    {
                        var expanded = new InternalEntry[internalNode.Length + 1];
                        internalNode.AsSpan().CopyTo(expanded);
                        expanded[expanded.Length - 1] = InternalEntry.CreateMutable(
                            updatedChild,
                            expanded.Length > 1 ? expanded[expanded.Length - 2].CumulativeChildCountForBuilder + 1 : 1
                        );
                        isSplit = false;
                        return expanded;
                    }

                    // case 3: "left-leaning" split
                    isSplit = true;
                    return new InternalEntry[] { InternalEntry.CreateMutable(updatedChild, 1) };
                }

                LeafEntry[] leafNode = Unsafe.As<LeafEntry[]>(node);

                // case 1: expand
                if (count < MaxLeafNodeSize)
                {
                    isSplit = false;
                    if (isMutable) // expand in place
                    {
                        leafNode[count].Item = item;
                        return leafNode;
                    }

                    var expandedLeaf = new LeafEntry[MaxLeafNodeSize];
                    leafNode.AsSpan().CopyTo(expandedLeaf);
                    expandedLeaf[leafNode.Length].Item = item;
                    return expandedLeaf;
                }

                // case 2: "left-leaning" split
                isSplit = true;
                var newLeaf = new LeafEntry[MaxLeafNodeSize];
                newLeaf[0].Item = item;
                return newLeaf;
            }

            public void Clear()
            {
                ++_version;
                _isRootMutable = false;
                _count = 0;
                _root = Array.Empty<LeafEntry>();
            }

            public bool Contains(T item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public Enumerator GetEnumerator() => new(_root, this);

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public int IndexOf(T item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, T item)
            {
                throw new NotImplementedException();
            }

            public bool Remove(T item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            #region IList members
            bool IList.IsFixedSize => false;

            bool IList.IsReadOnly => false;

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => false;

            object? IList.this[int index] { get => this[index]; set => this[index] = (T)value!; }

            int IList.Add(object? value)
            {
                Add((T)value!);
                return Count - 1;
            }

            bool IList.Contains(object? value) => IsCompatibleObject(value, out T cast) && Contains(cast);

            int IList.IndexOf(object? value) => IsCompatibleObject(value, out T cast) ? IndexOf(cast) : -1;

            void IList.Insert(int index, object? value) => Insert(index, (T)value!);

            void IList.Remove(object? value)
            {
                if (IsCompatibleObject(value, out T cast))
                {
                    Remove(cast);
                }
            }

            void ICollection.CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }
            #endregion

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

            public void CopyTo(T[] array)
            {
                throw new NotImplementedException();
            }

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
                throw new NotImplementedException();
            }

            public ArrayBasedBPlusTreeImmutableList<T> FindAll(Predicate<T> match)
            {
                throw new NotImplementedException();
            }

            public int FindIndex(Predicate<T> match)
            {
                throw new NotImplementedException();
            }

            public int FindIndex(int startIndex, Predicate<T> match)
            {
                throw new NotImplementedException();
            }

            public int FindIndex(int startIndex, int count, Predicate<T> match)
            {
                throw new NotImplementedException();
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
        }
    }
}
