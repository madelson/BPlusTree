using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using static Storage;

    public partial class NodeBasedBPlusTreeImmutableList<T>
    {
        internal sealed class LeafNode : Node
        {
            private Storage8<T> _items;

            internal int ChildCount { get; set; }

            internal override int Count => this.ChildCount;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref T Item(int index)
            {
                Debug.Assert(index < this.Count);
                return ref Get(ref this._items, index);
            }

            internal override (Node Updated, Node? Split) Insert(int index, T item)
            {
                var count = this.ChildCount;
                Debug.Assert(index >= 0 && index <= count);

                // case 1: expand
                if (count < NodeSize)
                {
                    LeafNode updated = new() { ChildCount = count + 1 };
                    for (var i = 0; i < index; ++i)
                    {
                        updated.Item(i) = this.Item(i);
                    }
                    updated.Item(index) = item;
                    for (int i = index; i < count; ++i)
                    {
                        updated.Item(i + 1) = this.Item(i);
                    }
                    return (updated, null);
                }

                // case 2: split
                LeafNode left = new() { ChildCount = LeftSplitNodeSize }, 
                    right = new() { ChildCount = RightSplitNodeSize };
                int insertionOffset = 0;
                for (var i = 0; i <= NodeSize; ++i)
                {
                    if (i == index)
                    {
                        insertionOffset = 1;
                        (i < LeftSplitNodeSize ? ref left.Item(i) : ref right.Item(i - LeftSplitNodeSize)) = item;
                    }
                    else
                    {
                        (i < LeftSplitNodeSize ? ref left.Item(i) : ref right.Item(i - LeftSplitNodeSize)) = this.Item(i - insertionOffset);
                    }
                }
                return (left, right);
            }
        }
    }
}
