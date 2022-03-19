using System;
using System.Collections;
using System.Collections.Generic;
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

        public sealed class Builder : IList<T>
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

            bool ICollection<T>.IsReadOnly => false;

            public ArrayBasedBPlusTreeImmutableList<T> ToImmutable()
            {
                if (_isRootMutable)
                {
                    Freeze(_root);
                    _isRootMutable = false;
                }

                return new(_root, _count);
            }

            private static void Freeze(Array node)
            {
                if (node.GetType() != typeof(InternalEntry[])) { return; }

                InternalEntry[] internalNode = Unsafe.As<InternalEntry[]>(node);
                for (var i = 0; i < internalNode.Length; ++i)
                {
                    if (internalNode[i].IsChildMutable)
                    {
                        Freeze(internalNode[i].Child);
                        internalNode[i].IsChildMutable = false;
                    }
                }
            }

            public void Add(T item)
            {
                throw new NotImplementedException();
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

            public IEnumerator<T> GetEnumerator()
            {
                int version = _version;
                for (var i = 0; i < _count; ++i)
                {
                    if (version != _version) { ThrowHelper.ThrowVersionChanged(); }
                    yield return this[i];
                }
            }

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
        }
    }
}
