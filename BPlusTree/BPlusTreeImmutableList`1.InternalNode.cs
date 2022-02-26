using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using static Storage;

    public partial class BPlusTreeImmutableList<T>
    {
        internal sealed class InternalNode : Node
        {
            internal int ChildrenCount { get; set; }
            internal Storage8<int> _cumulativeChildCounts;
            internal Storage8<Node?> _children;

            internal override int Count => this.CumulativeChildCount(this.ChildrenCount - 1);

            internal ref int CumulativeChildCount(int index)
            {
                Debug.Assert(index < this.ChildrenCount);
                return ref Get(ref this._cumulativeChildCounts, index);
            }

            internal ref Node Child(int index)
            {
                Debug.Assert(index < this.ChildrenCount);
                return ref Get(ref this._children, index)!;
            }

            internal override (Node Updated, Node? Split) Insert(int index, T item)
            {
                Debug.Assert(index >= 0 && index <= this.Count);

                int childrenCount = this.ChildrenCount;
                var lastCumulativeChildCount = 0;
                var childIndex = 0;
                var lastChildIndex = childrenCount - 1;
                do
                {
                    int cumulativeChildCount = this.CumulativeChildCount(childIndex);
                    if (index < cumulativeChildCount)
                    {
                        break;
                    }

                    lastCumulativeChildCount = cumulativeChildCount;
                    ++childIndex;
                }
                while (childIndex < lastChildIndex);

                var (updatedChild, splitChild) = this.Child(childIndex).Insert(index - lastCumulativeChildCount, item);

                // case 1: update
                if (splitChild is null)
                {
                    InternalNode updated = this.Copy(childrenCount);
                    updated.Child(childIndex) = updatedChild;
                    for (int i = childIndex; i < childrenCount; ++i)
                    {
                        ++updated.CumulativeChildCount(i);
                    }
                    return (updated, null);
                }

                // case 2: expand
                if (childrenCount < NodeSize)
                {
                    InternalNode expanded = new() { ChildrenCount = childrenCount + 1 };
                    int insertionOffset = 0;
                    int i = 0;
                    do
                    {
                        if (i == childIndex)
                        {
                            insertionOffset = 1;
                            expanded.Child(i) = updatedChild;
                            var updatedCumulativeChildCount = (i > 0 ? this.CumulativeChildCount(i - 1) : 0) + updatedChild.Count;
                            expanded.CumulativeChildCount(i) = updatedCumulativeChildCount;
                            ++i;
                            expanded.Child(i) = splitChild;
                            expanded.CumulativeChildCount(i) = updatedCumulativeChildCount + splitChild.Count;
                        }
                        else
                        {
                            expanded.Child(i) = this.Child(i - insertionOffset);
                            expanded.CumulativeChildCount(i) = this.CumulativeChildCount(i - insertionOffset) + 1;
                        }

                        ++i;
                    }
                    while (i <= childrenCount);

                    return (expanded, null);
                }

                // case 3: split
                {
                    InternalNode left = new() { ChildrenCount = LeftSplitNodeSize },
                        right = new() { ChildrenCount = RightSplitNodeSize };

                    for (var i = 0; i <= NodeSize; ++i)
                    {
                        InternalNode targetNode;
                        int targetIndex;
                        if (i < LeftSplitNodeSize)
                        {
                            targetNode = left;
                            targetIndex = i;
                        }
                        else
                        {
                            targetNode = right;
                            targetIndex = i - LeftSplitNodeSize;
                        }

                        Node child = i == childIndex ? updatedChild
                            : i == childIndex + 1 ? splitChild
                            : i > childIndex ? this.Child(i - 1)
                            : this.Child(i);
                        targetNode.Child(targetIndex) = child;
                        targetNode.CumulativeChildCount(targetIndex) =
                            child.Count + (targetIndex > 0 ? targetNode.CumulativeChildCount(targetIndex - 1) : 0);
                    }

                    return (left, right);
                }
            }

            public InternalNode Copy(int childrenCount)
            {
                Debug.Assert(childrenCount is > 0 and <= NodeSize);

                return new()
                {
                    ChildrenCount = childrenCount,
                    _cumulativeChildCounts = this._cumulativeChildCounts,
                    _children = this._children,
                };
            }
        }
    }
}
