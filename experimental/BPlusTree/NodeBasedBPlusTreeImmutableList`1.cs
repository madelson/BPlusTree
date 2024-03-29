﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using static Storage;

    public sealed partial class NodeBasedBPlusTreeImmutableList<T> : IReadOnlyList<T>
    {
        public static NodeBasedBPlusTreeImmutableList<T> Empty { get; } = new(new LeafNode(), count: 0);

        private readonly Node _root;
        private readonly int _count;

        internal NodeBasedBPlusTreeImmutableList(Node root, int count)
        {
            Debug.Assert(count >= 0);

            this._root = root;
            this._count = count;
        }

        public int Count => this._count;

        public T this[int index] => this.ItemRef(index);

        public ref readonly T ItemRef(int index)
        {
            if ((uint)index >= (uint)this._count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            Node current = this._root;
            while (true)
            {
                if (current is InternalNode internalNode)
                {
                    var lastCumulativeChildCount = 0;
                    var childIndex = 0;
                    var lastChildIndex = internalNode.ChildrenCount - 1;
                    do
                    {
                        int cumulativeChildCount = internalNode.CumulativeChildCount(childIndex);
                        if (index < cumulativeChildCount)
                        {
                            break;
                        }

                        lastCumulativeChildCount = cumulativeChildCount;
                        ++childIndex;
                    }
                    while (childIndex < lastChildIndex);

                    current = internalNode.Child(childIndex);
                    index -= lastCumulativeChildCount;
                }
                else // leaf
                {
                    Debug.Assert(index < NodeSize);
                    return ref ((LeafNode)current).Item(index);
                }
            }
        }

        public NodeBasedBPlusTreeImmutableList<T> Insert(int index, T item)
        {
            int count = this._count;
            // todo if index == count route through Add(item)
            if ((uint)index > (uint)count) { ThrowHelper.ThrowArgumentOutOfRange(); }

            (Node updated, Node? split) = this._root.Insert(index, item);
            Debug.Assert(updated.Count + (split?.Count ?? 0) == count + 1);
            
            if (split is null)
            {
                return new(updated, count + 1);
            }

            InternalNode root = new() { ChildrenCount = 2 };
            root.Child(0) = updated;
            root.CumulativeChildCount(0) = updated.Count;
            root.Child(1) = split;
            root.CumulativeChildCount(1) = count + 1;
            return new(root, count + 1);
        }

        // todo implement Add(). This is another flavor of insert optimized for adding at the
        // end. A difference is that splits with Add() are heavily left-weighted, leaving a full
        // left node and a right node of size 1. This is much more efficient if we are consecutively adding
        // since most nodes will be full instead of most being half-full. Add can also avoid walking the
        // cumulative counts list since we know we're always following the right branch, and it can
        // maybe extend leaf nodes more efficiently.

        public NodeBasedBPlusTreeImmutableList<T> AddRange(IEnumerable<T> items)
        {
            if (items is null) { ThrowHelper.ThrowArgumentNull(nameof(items)); }

            var immutableList = items as NodeBasedBPlusTreeImmutableList<T>;
            if (immutableList is not null)
            {
                if (this.Count == 0) { return immutableList; }
                if (immutableList.Count == 0) { return this; }

                // todo we can further optimize this case by recursing through
                // immutableList instead of iterating over it. When we land on a leaf:
                // if itemQueue is empty, just add the leaf unchanged to the leaf nodeQueue
                // if itemQueue.Count + leaf.Count <= NodeSize, merge the two leaves and add it to the leaf nodeQueue
                // else create two leaves that are at least half full and add them to the leaf node queue
                // With this approach, we can re-use most of the leaf nodes from the appended tree without even iterating
                // over their items.
            }

            using var enumerator = items.GetEnumerator();

            if (!enumerator.MoveNext()) { return this; }

            Deque<T> itemQueue = new();
            Deque<Deque<Node>> nodeQueues = new();

            if (!PrepopulateQueues(_root, itemQueue, nodeQueues))
            {
                nodeQueues.PeekTail().EnqueueTail(_root);
            }

            do
            {
                if (itemQueue.Count == NodeSize - 1)
                {
                    LeafNode leafNode = new() { ChildCount = NodeSize };
                    for (var i = 0; i < NodeSize - 1; ++i)
                    {
                        leafNode.Item(i) = itemQueue.DequeueHead();
                    }
                    leafNode.Item(NodeSize - 1) = enumerator.Current;
                    nodeQueues.PeekHead().EnqueueTail(leafNode);
                    int level = 0;
                    while (BuildInternalNodes(level++, threshold: NodeSize, nodeQueues)) ;
                }
                else
                {
                    itemQueue.EnqueueTail(enumerator.Current);
                }
            } while (enumerator.MoveNext());

            // collapse the leaf level
            if (itemQueue.Count > 0)
            {
                LeafNode leafNode = new() { ChildCount = itemQueue.Count };
                do
                {
                    leafNode.Item(itemQueue.Count - 1) = itemQueue.DequeueTail();
                }
                while (itemQueue.Count > 0);
                nodeQueues.PeekHead().EnqueueTail(leafNode);
            }
            // collapse all levels below the root
            for (var level = 0; level < nodeQueues.Count - 1; ++level)
            {
                BuildInternalNodes(level, threshold: 1, nodeQueues);
            }
            // collapse the root if there are multiple nodes at that level
            BuildInternalNodes(level: nodeQueues.Count - 1, threshold: 2, nodeQueues);

            Node root = nodeQueues.PeekTail().PeekHead();
            return new(root, root.Count);

            static bool PrepopulateQueues(Node node, Deque<T> itemQueue, Deque<Deque<Node>> nodeQueues)
            {
                bool result;
                if (node is InternalNode internalNode)
                {
                    int childrenCount = internalNode.ChildrenCount;
                    if (PrepopulateQueues(internalNode.Child(childrenCount - 1), itemQueue, nodeQueues))
                    {
                        for (var i = childrenCount - 2; i >= 0; --i)
                        {
                            nodeQueues.PeekTail().EnqueueHead(internalNode.Child(i));
                        }
                        result = true;
                    }
                    else if (childrenCount < NodeSize)
                    {
                        for (var i = 0; i < childrenCount; ++i)
                        {
                            nodeQueues[nodeQueues.Count - 1].EnqueueTail(internalNode.Child(i));
                        }
                        result = true;
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    var leafNode = (LeafNode)node;
                    int childCount = leafNode.ChildCount;
                    if (childCount < NodeSize)
                    {
                        for (var i = 0; i < childCount; ++i)
                        {
                            itemQueue.EnqueueTail(leafNode.Item(i));
                        }
                        result = true;
                    }
                    else
                    {
                        result = false;
                    }
                }

                nodeQueues.EnqueueTail(new());
                return result;
            }

            static bool BuildInternalNodes(int level, int threshold, Deque<Deque<Node>> nodeQueues)
            {
                var currentLevelNodeQueue = nodeQueues[level];
                bool shouldBuild = currentLevelNodeQueue.Count >= threshold;
                if (!shouldBuild)
                {
                    return false;
                }
                do
                {
                    int childrenCount = Math.Min(currentLevelNodeQueue.Count, NodeSize);
                    var internalNode = new InternalNode { ChildrenCount = childrenCount };
                    for (var i = 0; i < childrenCount; ++i)
                    {
                        Node child = currentLevelNodeQueue.DequeueHead();
                        internalNode.Child(i) = child;
                        internalNode.CumulativeChildCount(i) = child.Count + (i > 0 ? internalNode.CumulativeChildCount(i - 1) : 0);
                    }
                    EnsureNodeQueue(nodeQueues, level + 1).EnqueueTail(internalNode);
                }
                while (currentLevelNodeQueue.Count >= threshold);

                return true;
            }

            static Deque<Node> EnsureNodeQueue(Deque<Deque<Node>> nodeQueues, int index)
            {
                Debug.Assert(index >= 0 && index <= nodeQueues.Count);

                Deque<Node> nodeQueue;
                if (nodeQueues.Count == index)
                {
                    nodeQueues.EnqueueTail(nodeQueue = new());
                }
                else
                {
                    nodeQueue = nodeQueues[index];
                }
                return nodeQueue;
            }
        }

        // todo use strong enumerator instead
        // todo use stack-based implementation
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < this.Count; ++i)
            {
                yield return this[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
