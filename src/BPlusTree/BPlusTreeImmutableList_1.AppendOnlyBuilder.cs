using System;
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Medallion.Collections;

public partial class BPlusTreeImmutableList<T>
{
    private struct AppendOnlyBuilder : IDisposable
    {
        private readonly NodeBuilder<IndexEntry>[] _indexBuilders;
        private NodeBuilder<LeafEntry> _leafBuilder;
        private readonly LeafEntry[] _rentedLeafBuffer;
        private readonly IndexEntry[] _rentedIndexBuffer;
        private int _indexLevels;
        private int _lowestPopulatedIndexLevel = int.MaxValue;
        private bool _needsTrailingEdgeExpanded;

        public AppendOnlyBuilder()
        {
            const int MaxIndexLevels = 16; // TODO revisit

            _rentedLeafBuffer = ArrayPool<LeafEntry>.Shared.Rent(MaxLeafNodeSize);
            _rentedIndexBuffer = ArrayPool<IndexEntry>.Shared.Rent(MaxIndexNodeSize * MaxIndexLevels);
            _indexBuilders = ArrayPool<NodeBuilder<IndexEntry>>.Shared.Rent(MaxIndexLevels);
            _leafBuilder = new(_rentedLeafBuffer, bufferStartIndex: 0);

            for (var i = 0; i < MaxIndexLevels; ++i)
            {
                _indexBuilders[i] = new(_rentedIndexBuffer, bufferStartIndex: i * MaxIndexNodeSize);
            }
        }

        public Array MoveToNode(out int count, out bool isNodeMutable)
        {
            if (_indexLevels == 0)
            {
                count = _leafBuilder.Count;
                if (count == 0)
                {
                    isNodeMutable = false;
                    return Empty._root;
                }
                return _leafBuilder.MoveToNode(out isNodeMutable);
            }

            // complete everything below the top level
            if (_leafBuilder.Count > 0)
            {
                CompleteLeaf();
            }
            for (var i = 0; i < _indexLevels - 1; ++i)
            {
                if (_indexBuilders[i].Count > 0)
                {
                    // todo mutable
                    IndexEntry[] completedNode = _indexBuilders[i].MoveToNode(out _);
                    CompleteNode(completedNode, count: GetCount(completedNode), indexLevel: i + 1);
                }
            }

            ref NodeBuilder<IndexEntry> maxLevelBuilder = ref _indexBuilders[_indexLevels - 1];

            // if the top level would be a single-element index, then its child should be the root instead
            Debug.Assert(maxLevelBuilder.Count > 1); // TODO can simplify
            if (maxLevelBuilder.Count == 1)
            {
                Debug.Assert(maxLevelBuilder.Count == 2); // todo
                IndexEntry rootEntry = maxLevelBuilder[0];
                count = rootEntry.Offset; // todo builder
                isNodeMutable = false; // todo mutable
                return rootEntry.Child;
            }

            IndexEntry[] completedRoot = maxLevelBuilder.MoveToNode(out isNodeMutable);
            count = GetCount(completedRoot);
            return completedRoot;
        }

        public void Add(T item)
        {
            if (_leafBuilder.Count == MaxLeafNodeSize)
            {
                CompleteLeaf();
            }
            else if (_needsTrailingEdgeExpanded)
            {
                ExpandTrailingEdge();
            }

            _leafBuilder.Add(new(item));
            _lowestPopulatedIndexLevel = -1;
        }

        private void CompleteLeaf()
        {
            // todo revisit isNodeMutable for builder
            LeafEntry[] leaf = _leafBuilder.MoveToNode(out bool isNodeMutable);
            CompleteNode(leaf, count: leaf.Length, indexLevel: 0);
        }

        private void CompleteNode(Array node, int count, int indexLevel)
        {
            ref NodeBuilder<IndexEntry> indexBuilder = ref _indexBuilders[indexLevel];

            if (indexBuilder.Count == MaxIndexNodeSize)
            {
                // todo revisit isNodeMutable for builder
                IndexEntry[] completedIndex = indexBuilder.MoveToNode(out _);
                CompleteNode(completedIndex, GetCount(completedIndex), indexLevel + 1);
            }

            _lowestPopulatedIndexLevel = indexLevel;

            if (indexBuilder.Count == 0)
            {
                indexBuilder.Add(new(node, count));
                // if this level was previously empty, then it might be the new highest level
                if (indexLevel == _indexLevels)
                {
                    _indexLevels = indexLevel + 1;
                }
                return;
            }

            int offset = indexBuilder.Last.Offset + count;
            indexBuilder.Add(new(node, offset));
        }

        public void AddNode(LeafEntry[] leaf)
        {
            Debug.Assert(leaf.Length > 0 && leaf.Length <= MaxLeafNodeSize);

            if (_leafBuilder.Count >= MinLeafNodeSize)
            {
                CompleteLeaf();
                // todo revisit isNodeMutable for builder
                _leafBuilder.SetNode(leaf, mutable: false);
                goto CODA;
            }

            if (_leafBuilder.Count == 0 && _needsTrailingEdgeExpanded)
            {
                ExpandTrailingEdge();
            }

            if (_leafBuilder.Count == 0)
            {
                // todo revisit isNodeMutable for builder
                _leafBuilder.SetNode(leaf, mutable: false);
                goto CODA;
            }

            // If the new elements won't fit inside the current leaf node, then
            // endeavor to leave behind a completable leaf node so that future
            // calls to this method will be set up for node reuse.
            if (_leafBuilder.Count + leaf.Length > MaxLeafNodeSize)
            {
                int initialAddCount = leaf.Length - MinLeafNodeSize;
                var i = 0;
                do
                {
                    _leafBuilder.Add(leaf[i++]);
                }
                while (i < initialAddCount);

                Debug.Assert(_leafBuilder.Count >= MinLeafNodeSize);
                CompleteLeaf();

                while (i < leaf.Length)
                {
                    Add(leaf[i++].Item);
                }

                Debug.Assert(_leafBuilder.Count == MinLeafNodeSize);
                goto CODA;
            }

            foreach (LeafEntry entry in leaf)
            {
                Add(entry.Item);
            }

        CODA:
            _lowestPopulatedIndexLevel = -1;
        }

        public void AddNode(IndexEntry[] index, bool isTrailingEdge = true, int indexLevel = -2) =>
            AddNode(index, isTrailingEdge, indexLevel, isExternalCall: true);

        private void AddNode(IndexEntry[] index, bool isTrailingEdge, int indexLevel, bool isExternalCall)
        {
            Debug.Assert(index.Length > 0 && index.Length <= MaxIndexNodeSize);
            Debug.Assert((indexLevel == -2 && isExternalCall) || indexLevel == CalculateIndexLevel(index));

            if (isExternalCall)
            {
                if (indexLevel == -2)
                {
                    indexLevel = CalculateIndexLevel(index);
                }

                // if current tree is empty, just add the new tree as a complete node
                if (_indexLevels == 0 && _leafBuilder.Count == 0)
                {
                    SetNode(index, isTrailingEdge, indexLevel);
                    _indexLevels = indexLevel + 1;
                    return;
                }

                if (_needsTrailingEdgeExpanded)
                {
                    ExpandTrailingEdge();
                }
            }

            // If the current tree is taller or the same height as the added tree, we might be able to use
            // the provided node as-is.
            if (indexLevel < _lowestPopulatedIndexLevel)
            {
                SetNode(index, isTrailingEdge, indexLevel);
                return;
            }

            if (indexLevel == _lowestPopulatedIndexLevel)
            {
                ref NodeBuilder<IndexEntry> indexBuilder = ref _indexBuilders[indexLevel];
                if (indexBuilder.Count >= MinIndexNodeSize)
                {
                    IndexEntry[] completedNode = indexBuilder.MoveToNode(out _); // todo mutable
                    CompleteNode(completedNode, count: GetCount(completedNode), indexLevel + 1);
                }

                if (indexBuilder.Count == 0)
                {
                    SetNode(index, isTrailingEdge, indexLevel);
                    return;
                }

                // If the new elements won't fit inside the current index node, then
                // endeavor to leave behind a completable index node so that future
                // calls to this method will be set up for node reuse.
                if (indexBuilder.Count + index.Length > MaxIndexNodeSize)
                {
                    int offset = indexBuilder.Last.Offset;
                    int initialAddCount = index.Length - MinIndexNodeSize;
                    for (var i = 0; i < index.Length; ++i)
                    {
                        if (i == initialAddCount)
                        {
                            Debug.Assert(indexBuilder.Count >= MinIndexNodeSize);
                            IndexEntry[] completedNode = indexBuilder.MoveToNode(out _); // todo mutable
                            CompleteNode(completedNode, count: GetCount(completedNode), indexLevel + 1);
                            offset = -index[i - 1].Offset;
                        }

                        IndexEntry entry = index[i];
                        indexBuilder.Add(new(entry.Child, entry.Offset + offset));
                    }

                    Debug.Assert(indexBuilder.Count == MinIndexNodeSize);
                    _lowestPopulatedIndexLevel = indexLevel;
                    _needsTrailingEdgeExpanded = isTrailingEdge;
                    return;
                }
            }

            // TODO mutable if this node is mutable and after adding the first child its node is still there
            // we can mutate the current node instead of adding the rest of the children
            // Otherwise, we just recursively add the children
            for (var i = 0; i < index.Length; ++i)
            {
                AddNode(index[i].Child, isTrailingEdge && i == index.Length - 1, indexLevel - 1, isExternalCall: false);
            }
        }

        private void ExpandTrailingEdge()
        {
            Debug.Assert(_needsTrailingEdgeExpanded);
            Debug.Assert(_lowestPopulatedIndexLevel >= 0);
            Debug.Assert(_indexLevels > 0);
            Debug.Assert(_leafBuilder.Count == 0);

            // first, determine the range of levels to be expanded
            var expansionEndLevel = int.MaxValue;
            {
                Array lastChild = _indexBuilders[_lowestPopulatedIndexLevel].Last.Child;
                for (int i = _lowestPopulatedIndexLevel; i >= 0; --i)
                {
                    if (lastChild.IsIndexNode())
                    {
                        if (lastChild.Length < MinIndexNodeSize)
                        {
                            expansionEndLevel = i;
                        }
                        IndexEntry[] lastChildIndex = lastChild.ToIndex();
                        lastChild = lastChildIndex[lastChildIndex.Length - 1].Child;
                    }
                    else
                    {
                        if (lastChild.Length < MinLeafNodeSize)
                        {
                            expansionEndLevel = 0;
                        }
                        break;
                    }
                }
            }

            for (int i = _lowestPopulatedIndexLevel; i >= expansionEndLevel; --i)
            {
                ref NodeBuilder<IndexEntry> indexBuilder = ref _indexBuilders[i];
                Array lastChild = indexBuilder.Last.Child;

                // Pop off the last child and add it to the next level's builder
                indexBuilder.Pop();
                if (i == 0)
                {
                    // todo mutable
                    _leafBuilder.SetNode(ToLeaf(lastChild), mutable: false);
                }
                else
                {
                    // todo mutable
                    _indexBuilders[i - 1].SetNode(lastChild.ToIndex(), mutable: false);
                }
            }

            // todo can we clean up this flow?
            if (expansionEndLevel <= _lowestPopulatedIndexLevel)
            {
                _lowestPopulatedIndexLevel = expansionEndLevel - 1;
            }

            _needsTrailingEdgeExpanded = false;
        }

        private void SetNode(IndexEntry[] index, bool isTrailingEdge, int indexLevel)
        {
            Debug.Assert(!_needsTrailingEdgeExpanded);
            Debug.Assert(indexLevel < _lowestPopulatedIndexLevel);
            Debug.Assert(_indexBuilders[indexLevel].Count == 0);

            // todo mutable
            _indexBuilders[indexLevel].SetNode(index, mutable: false);
            _needsTrailingEdgeExpanded = isTrailingEdge;
            _lowestPopulatedIndexLevel = indexLevel;
        }

        public void AddNode(Array node, bool isTrailingEdge = true, int indexLevel = -2) => 
            AddNode(node, isTrailingEdge, indexLevel, isExternalCall: true);

        private void AddNode(Array node, bool isTrailingEdge, int indexLevel, bool isExternalCall)
        {
            if (node.IsIndexNode())
            {
                AddNode(node.ToIndex(), isTrailingEdge, indexLevel, isExternalCall);
            }
            else
            {
                Debug.Assert(indexLevel is -2 or -1);
                AddNode(ToLeaf(node));
            }
        }

        public static int CalculateIndexLevel(Array node)
        {
            var indexLevel = -1;
            while (node.IsIndexNode())
            {
                ++indexLevel;
                node = node.ToIndex()[0].Child;
            }
            return indexLevel;
        }

        private static int CalculateIndexLevel(IndexEntry[] index) => 1 + CalculateIndexLevel(index[0].Child);

        public void Dispose()
        {
            ArrayPool<IndexEntry>.Shared.Return(_rentedIndexBuffer, clearArray: true);
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1
            ArrayPool<LeafEntry>.Shared.Return(_rentedLeafBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
#else
            ArrayPool<LeafEntry>.Shared.Return(_rentedLeafBuffer, clearArray: true);
#endif
            ArrayPool<NodeBuilder<IndexEntry>>.Shared.Return(_indexBuilders, clearArray: true);
        }
    }
}

[DebuggerDisplay("{DebugView}")]
internal struct NodeBuilder<T>
{
    private readonly T[] _buffer;
    private T[]? _node;
    private readonly int _bufferStartIndex;
    private int _count;
    private bool _isNodeMutable;

    public NodeBuilder(T[] buffer, int bufferStartIndex)
    {
        _buffer = buffer;
        _bufferStartIndex = bufferStartIndex;
    }

    public int Count => _count;

    public T Last => this[_count - 1];

    public T this[int index]
    {
        get
        {
            Debug.Assert((uint)index < (uint)_count);

            return _node is { } node ? node[index] : _buffer[_bufferStartIndex + index];
        }
        set
        {
            Debug.Assert((uint)index < (uint)_count);

            if (_node is not null)
            {
                if (_isNodeMutable)
                {
                    _node[index] = value;
                    return;
                }

                CopyNodeToBuffer();
            }

            _buffer[_bufferStartIndex + index] = value;
        }
    }

    public void Add(T item)
    {
        if (_node is { } node)
        {
            if (_count < node.Length)
            {
                Debug.Assert(_isNodeMutable);
                node[_count++] = item;
                return;
            }

            CopyNodeToBuffer();
        }

        _buffer[_bufferStartIndex + _count++] = item;
    }

    private void CopyNodeToBuffer()
    {
        _node!.AsSpan().CopyTo(_buffer.AsSpan(_bufferStartIndex));
        _node = null;
    }

    public void SetNode(T[] node, bool mutable)
    {
        Debug.Assert(_node is null && _count == 0);

        _node = node;
        _count = node.Length;
        _isNodeMutable = mutable;
    }

    public void Pop()
    {
        Debug.Assert(_count > 0);

        if (_node is not null && !_isNodeMutable)
        {
            CopyNodeToBuffer();
        }

        --_count;
    }

    public T[] MoveToNode(out bool isNodeMutable)
    {
        Debug.Assert(_count > 0);

        if (_node is { } node)
        {
            Debug.Assert(_count == node.Length);
            _node = null;
            isNodeMutable = _isNodeMutable;
        }
        else
        {
            node = _buffer.AsSpan(_bufferStartIndex, _count).ToArray();
            isNodeMutable = true;
        }

        _count = 0;
        return node;
    }

    private string DebugView
    {
        get
        {
            StringBuilder builder = new($"Count = {_count}");
            if (_node is not null)
            {
                builder.Append($" (existing{(_isNodeMutable ? " MUTABLE" : string.Empty)} node)");
            }
            return builder.ToString();
        }
    }
}
