using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    using static Storage;

    public partial class BPlusTreeImmutableList<T>
    {
        internal sealed class InternalNode : Node
        {
            internal int ChildrenCount { get; private set; }
            internal Storage7<int> ChildrenCounts;
            internal Storage8<Node?> Children;

            internal ref int ChildCount(int index)
            {
                Debug.Assert(index < this.ChildrenCount - 1);
                return ref Get(ref this.ChildrenCounts, index);
            }

            internal ref Node Child(int index)
            {
                Debug.Assert(index < this.ChildrenCount);
                return ref Get(ref this.Children, index)!;
            }

            internal override (Node Updated, Node? Split, int UpdatedCount) Insert(T item, int index, int count)
            {
                int childrenCount = this.ChildrenCount;
                var (childIndex, childCount, indexOffset) = this.FindChild(index, count, childrenCount);

                var (updatedChild, splitChild, updatedChildCount) = this.Child(childIndex).Insert(item, index - indexOffset, childCount);

                if (splitChild is null)
                {
                    InternalNode updated = new()
                    {
                        ChildrenCount = childrenCount,
                        ChildrenCounts = this.ChildrenCounts,
                        Children = this.Children,
                    };
                    updated.Child(childIndex) = updatedChild;
                    if (childIndex < childrenCount - 1)
                    {
                        updated.ChildCount(childIndex) = updatedChildCount;
                    }
                    return (updated, null, count + 1);
                }

                if (childrenCount < NodeSize)
                {
                    InternalNode updated = new() { ChildrenCount = childrenCount + 1 };
                    int insertionOffset = 0;
                    for (var i = 0; i <= childrenCount; ++i)
                    {
                        if (i == childIndex)
                        {
                            updated.Child(i) = updatedChild;
                            updated.ChildCount(i) = updatedChildCount;
                        }
                        else if (i == childIndex + 1)
                        {
                            insertionOffset = 1;
                            updated.Child(i) = splitChild;
                            if (i < childrenCount)
                            {
                                updated.ChildCount(i) = childCount + 1 - updatedChildCount;
                            }
                        }
                        else
                        {
                            updated.Child(i + insertionOffset) = this.Child(i);
                            if (i < childrenCount)
                            {
                                updated.ChildCount(i + insertionOffset) = this.ChildCount(i);
                            }
                        }
                    }
                    return (updated, null, count + 1);
                }

                {
                    InternalNode left = new() { ChildrenCount = SplitNodeSize },
                        right = new() { ChildrenCount = SplitNodeSize };
                    var insertionOffset = 0;
                    var leftCount = 0;
                    for (var i = 0; i < NodeSize; ++i)
                    {
                        if (i == childIndex)
                        {
                            if (i < SplitNodeSize)
                            {
                                left.Child(i) = updatedChild;
                                if (i < SplitNodeSize - 1)
                                {
                                    leftCount += left.ChildCount(i) = updatedChildCount;
                                }
                            }
                            else
                            {
                                right.Child(i - SplitNodeSize) = updatedChild;
                                if (i < NodeSize - 1)
                                {
                                    right.ChildCount(i) = updatedChildCount;
                                }
                            }
                        }
                        else if (i == childIndex + 1)
                        {
                            insertionOffset = 1;
                            if (i < SplitNodeSize)
                            {
                                left.Child(i) = splitChild;
                                if (i < SplitNodeSize - 1)
                                {
                                    leftCount += left.ChildCount(i) = childCount + 1 - updatedChildCount;
                                }
                            }
                            else
                            {
                                right.Child(i - SplitNodeSize) = splitChild;
                                if (i < NodeSize - 1)
                                {
                                    right.ChildCount(i) = childCount + 1 - updatedChildCount;
                                }
                            }
                        }
                        else
                        {
                            if (i < SplitNodeSize)
                            {
                                left.Child(i) = this.Child(i);
                                if (i < SplitNodeSize - 1)
                                {
                                    left.ChildCount(i) = this.ChildCount(i);
                                }
                            }
                            else
                            {
                                right.Child(i - SplitNodeSize) = splitChild;
                                if (i < NodeSize - 1)
                                {
                                    right.ChildCount(i) = childCount + 1 - updatedChildCount;
                                }
                            }
                        }
                    }
                }
            }

            private (int ChildIndex, int ChildCount, int IndexOffset) FindChild(int index, int count, int childrenCount)
            {
                var indexOffset = 0;
                for (var i = 0; i < childrenCount - 1; ++i)
                {
                    int childCount = this.ChildCount(i);
                    if (index < childCount)
                    {
                        return (ChildIndex: i, ChildCount: childCount, IndexOffset: indexOffset);
                    }
                    else
                    {
                        indexOffset += childCount;
                    }
                }
                return (ChildIndex: childrenCount - 1, ChildCount: count - indexOffset, IndexOffset: indexOffset);
            }
        }
    }
}
