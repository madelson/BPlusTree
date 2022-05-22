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
    using InternalNodeStack = Stack<(ArrayBasedBPlusTreeImmutableListInternalEntry[] Node, int Index)>;

    public partial class ArrayBasedBPlusTreeImmutableList<T>
    {
        public Enumerator GetEnumerator() => new(_root, builder: null);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>, ISecurePooledObjectUser, IStrongEnumerator<T>
        {
            private Builder? _builder;
            private SecurePooledObject<InternalNodeStack>? _internalNodeStack;
            private LeafEntry[]? _leaf;
            private int _leafIndex;
            private int _builderVersion;
            private int _poolUserId;

            internal Enumerator(Array node, Builder? builder)
            {
                _builder = builder;
                _builderVersion = builder is null ? 0 : builder.Version;
                _internalNodeStack = null;
                _leafIndex = -1;

                if (node.GetType() == typeof(InternalEntry[]))
                {
                    _leaf = null;
                    _poolUserId = SecureObjectPool.NewId();
                    if (!SecureObjectPool<InternalNodeStack, Enumerator>.TryTake(this, out _internalNodeStack))
                    {
                        _internalNodeStack = SecureObjectPool<InternalNodeStack, Enumerator>.PrepNew(this, new InternalNodeStack());
                    }
                    Debug.Assert(_internalNodeStack!.Use(ref this).Count == 0);
                    _internalNodeStack!.Use(ref this).Push((Unsafe.As<InternalEntry[]>(node), -1));
                }
                else
                {
                    _poolUserId = -1;
                    _leaf = Unsafe.As<LeafEntry[]>(node);
                }
            }

            public T Current
            {
                get
                {
                    ThrowIfDisposed();
                    return (_leaf ?? throw new InvalidOperationException())[_leafIndex].Item;
                }
            }

            object? IEnumerator.Current => Current;

            public void Dispose()
            {
                _leaf = null;
                if (_internalNodeStack != null && _internalNodeStack.TryUse(ref this, out var stack))
                {
                    stack.Clear();
                    SecureObjectPool<InternalNodeStack, Enumerator>.TryAdd(this, _internalNodeStack!);
                }
                _internalNodeStack = null;
            }

            public bool MoveNext()
            {
                ThrowIfDisposed();
                if (_builder != null)
                {
                    return BuilderMoveNext();
                }

                if (_leaf is { } leaf && _leafIndex < leaf.Length - 1)
                {
                    ++_leafIndex;
                    return true;
                }

                return NavigateToNextLeaf();
            }

            private bool BuilderMoveNext()
            {
                if (_builder!.Version != _builderVersion) 
                { 
                    ThrowHelper.ThrowVersionChanged(); 
                }
                
                if (_leaf is { } leaf)
                {
                    int leafCount;
                    InternalNodeStack internalNodeStack;
                    if (_internalNodeStack is null 
                        || (internalNodeStack = _internalNodeStack!.Use(ref this)).Count == 0)
                    {
                        leafCount = _builder!.Count;
                    }
                    else
                    {
                        var (parentNode, parentNodeIndex) = internalNodeStack.Peek();
                        leafCount = parentNodeIndex == 0
                            ? parentNode[parentNodeIndex].CumulativeChildCountForBuilder
                            : parentNode[parentNodeIndex].CumulativeChildCountForBuilder - parentNode[parentNodeIndex - 1].CumulativeChildCountForBuilder;
                    }

                    if (_leafIndex < leafCount - 1)
                    {
                        ++_leafIndex;
                        return true;
                    }
                }

                return NavigateToNextLeaf();
            }

            private bool NavigateToNextLeaf()
            {
                // single-leaf tree where we've exhausted the leaf
                if (_internalNodeStack is null)
                {
                    _leafIndex = _leaf!.Length;
                    return false;
                }

                InternalNodeStack internalNodeStack = _internalNodeStack!.Use(ref this);
                var current = internalNodeStack.Pop();
                ++current.Index;
                while (true)
                {
                    // current node incomplete -> traverse to child
                    if (current.Index < current.Node.Length)
                    {
                        internalNodeStack.Push(current);
                        Array child = current.Node[current.Index].Child;
                        if (child.GetType() == typeof(InternalEntry[]))
                        {
                            current = (Unsafe.As<InternalEntry[]>(child), 0);
                        }
                        else
                        {
                            _leaf = Unsafe.As<LeafEntry[]>(child);
                            _leafIndex = 0;
                            return true;
                        }
                    }
                    // current node complete -> backtrack to parent
                    else if (internalNodeStack.Count > 0)
                    {
                        current = internalNodeStack.Pop();
                        ++current.Index;
                    }
                    // root node completed -> iteration complete
                    else
                    {
                        internalNodeStack.Push((current.Node, current.Node.Length));
                        _leaf = null;
                        return false;
                    }
                }
            }

            public void Reset()
            {
                ThrowIfDisposed();

                if (_builder is { } builder)
                {
                    _builderVersion = builder.Version;
                }

                _leafIndex = -1;

                if (_internalNodeStack != null)
                {
                    _leaf = null;
                    InternalNodeStack internalNodeStack = _internalNodeStack!.Use(ref this);
                    (InternalEntry[] Node, int Index) current;
                    do 
                    { 
                        current = internalNodeStack.Pop(); 
                    }
                    while (internalNodeStack.Count > 0);
                    internalNodeStack.Push((current.Node, -1));
                }
            }

            int ISecurePooledObjectUser.PoolUserId => _poolUserId;

            private void ThrowIfDisposed()
            {
                if (_internalNodeStack is null && _leaf is null)
                {
                    ThrowHelper.ThrowObjectDisposed(this);
                }
            }
        }
    }
}
